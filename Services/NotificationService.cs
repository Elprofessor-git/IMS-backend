using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Service de création des notifications de l'utilisateur (cloche) pour les nouveaux
    /// émetteurs : tâches et courriels.
    ///
    /// Ce service n'est pas un second système : il écrit dans la table <c>Notifications</c>
    /// déjà consommée par <c>NotificationController</c> et par la cloche du frontend, avec
    /// les mêmes règles que l'émetteur historique (planning) :
    ///   • un destinataire = un utilisateur du système décidé côté serveur ;
    ///   • aucune information sensible dans le message (jamais de corps d'email, de token
    ///     OAuth ni de donnée d'autrui) ;
    ///   • best effort : l'échec d'une notification ne fait jamais échouer l'opération métier.
    ///
    /// Il centralise en plus la déduplication, pour qu'une même opération ne produise pas
    /// deux notifications identiques (règle §8 du LOT 17).
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Notifie le changement de responsable d'une tâche (assignation, réassignation ou
        /// désassignation).
        /// </summary>
        /// <param name="tache">Tâche concernée, déjà enregistrée (Id attribué).</param>
        /// <param name="ancienResponsableUserId">
        /// Responsable AVANT l'opération (null si la tâche n'en avait pas). C'est lui qui est
        /// prévenu d'une désassignation ; un changement sans ancien responsable n'informe
        /// donc personne de la perte.
        /// </param>
        /// <param name="auteurUserId">Utilisateur ayant déclenché l'opération (claims JWT).</param>
        /// <param name="cancellationToken">Annulation de l'appel HTTP.</param>
        Task NotifierAssignationAsync(
            TacheProduction tache,
            string? ancienResponsableUserId,
            string auteurUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifie le responsable d'une tâche née d'un email (validation humaine de la
        /// suggestion IA). Le message ne contient QUE le titre de la tâche : jamais le corps
        /// du courrier, qui peut être confidentiel.
        /// </summary>
        /// <param name="tache">Tâche créée depuis l'email, déjà enregistrée.</param>
        /// <param name="auteurUserId">Utilisateur ayant validé l'email (claims JWT).</param>
        /// <param name="gmailMessageId">Email d'origine, cible du lien de retour.</param>
        /// <param name="cancellationToken">Annulation de l'appel HTTP.</param>
        Task NotifierTacheDepuisEmailAsync(
            TacheProduction tache,
            string auteurUserId,
            int gmailMessageId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifie un utilisateur qu'il a été mentionné (@) dans une discussion.
        /// Le message ne contient QUE le nom de l'auteur et le nom de la conversation :
        /// jamais le contenu du message, qui peut être confidentiel. Best effort et
        /// dédupliqué par (destinataire, message de discussion).
        /// </summary>
        /// <param name="destinataireUserId">Utilisateur mentionné (jamais l'auteur).</param>
        /// <param name="auteurUserId">Utilisateur ayant écrit la mention (claims JWT).</param>
        /// <param name="conversationId">Conversation où la mention a été écrite.</param>
        /// <param name="nomConversation">Libellé affichable de la conversation.</param>
        /// <param name="messageId">Message contenant la mention, cible de la déduplication.</param>
        /// <param name="cancellationToken">Annulation de l'appel HTTP.</param>
        Task NotifierMentionAsync(
            string destinataireUserId,
            string auteurUserId,
            int conversationId,
            string nomConversation,
            long messageId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifie l'auteur d'un message qu'on lui a répondu (module Discussion, E3).
        /// Même contrat que la mention (jamais de contenu, best effort, dédupliqué par
        /// destinataire et message de discussion).
        /// </summary>
        /// <param name="destinataireUserId">Auteur du message d'origine (jamais le répondeur).</param>
        /// <param name="auteurUserId">Utilisateur qui a répondu (claims JWT).</param>
        /// <param name="conversationId">Conversation où la réponse a été écrite.</param>
        /// <param name="nomConversation">Libellé affichable de la conversation.</param>
        /// <param name="messageId">Le message RÉPONSE, cible de la déduplication.</param>
        /// <param name="cancellationToken">Annulation de l'appel HTTP.</param>
        Task NotifierReponseAsync(
            string destinataireUserId,
            string auteurUserId,
            int conversationId,
            string nomConversation,
            long messageId,
            CancellationToken cancellationToken = default);
    }

    public class NotificationService : INotificationService
    {
        /// <summary>Le titre est Tronqué pour que le message reste lisible et tienne en 500 caractères.</summary>
        private const int LongueurMaxTitre = 120;

        private readonly ApplicationDbContext _db;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext db, ILogger<NotificationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task NotifierAssignationAsync(
            TacheProduction tache,
            string? ancienResponsableUserId,
            string auteurUserId,
            CancellationToken cancellationToken = default) =>
            ExecuterAsync(
                nouveauResponsableUserId: tache.AssignedToUserId,
                ancienResponsableUserId: ancienResponsableUserId,
                auteurUserId: auteurUserId,
                type: TypeNotification.TacheAssignee,
                tache: tache,
                gmailMessageId: null,
                message: $"Une tâche vous a été assignée : « {Titre(tache)} »",
                messageRetrait: $"La tâche « {Titre(tache)} » ne vous est plus assignée.",
                cancellationToken: cancellationToken);

        public Task NotifierTacheDepuisEmailAsync(
            TacheProduction tache,
            string auteurUserId,
            int gmailMessageId,
            CancellationToken cancellationToken = default) =>
            ExecuterAsync(
                nouveauResponsableUserId: tache.AssignedToUserId,
                ancienResponsableUserId: null,
                auteurUserId: auteurUserId,
                type: TypeNotification.TacheDepuisEmail,
                tache: tache,
                gmailMessageId: gmailMessageId,
                message: $"Une nouvelle tâche a été créée depuis un email : « {Titre(tache)} »",
                messageRetrait: null,
                cancellationToken: cancellationToken);

        public Task NotifierMentionAsync(
            string destinataireUserId,
            string auteurUserId,
            int conversationId,
            string nomConversation,
            long messageId,
            CancellationToken cancellationToken = default) =>
            NotifierDiscussionAsync(
                destinataireUserId,
                auteurUserId,
                conversationId,
                nomConversation,
                messageId,
                TypeNotification.MentionDiscussion,
                "Vous avez été mentionné par {0} dans « {1} ».",
                cancellationToken);

        public Task NotifierReponseAsync(
            string destinataireUserId,
            string auteurUserId,
            int conversationId,
            string nomConversation,
            long messageId,
            CancellationToken cancellationToken = default) =>
            NotifierDiscussionAsync(
                destinataireUserId,
                auteurUserId,
                conversationId,
                nomConversation,
                messageId,
                TypeNotification.ReponseDiscussion,
                "{0} a répondu à votre message dans « {1} ».",
                cancellationToken);

        /// <summary>
        /// Écrit une notification de discussion (mention OU réponse), best effort et
        /// dédupliquée par (destinataire, type, message). Ne contient jamais le contenu
        /// du message : seulement le nom de l'auteur et le libellé de la conversation.
        /// </summary>
        private async Task NotifierDiscussionAsync(
            string destinataireUserId,
            string auteurUserId,
            int conversationId,
            string nomConversation,
            long messageId,
            TypeNotification type,
            string modele,
            CancellationToken cancellationToken)
        {
            try
            {
                // Auto-notification inutile : l'auteur sait qu'il vient d'écrire.
                if (string.IsNullOrEmpty(destinataireUserId) || destinataireUserId == auteurUserId)
                    return;

                // Défense en profondeur : jamais de notification vers un compte
                // inexistant ou inactif (voir EstDestinataireValideAsync).
                if (!await EstDestinataireValideAsync(destinataireUserId, cancellationToken))
                {
                    _logger.LogWarning(
                        "Notification {Type} ignorée : destinataire {Destinataire} inexistant ou inactif (conversation {ConversationId}).",
                        type, destinataireUserId, conversationId);
                    return;
                }

                // Déduplication : un message modifié ne doit pas re-notifier la même
                // personne à chaque réécriture.
                if (await _db.Notifications.AnyAsync(n =>
                        n.UtilisateurId == destinataireUserId
                        && n.Type == type
                        && n.MessageDiscussionId == messageId
                        && !n.EstLivree,
                        cancellationToken))
                {
                    return;
                }

                // Nom de l'auteur, borné, jamais de donnée sensible.
                var auteur = await _db.Users
                    .Where(u => u.Id == auteurUserId)
                    .Select(u => new { u.Prenom, u.Nom, u.UserName })
                    .FirstOrDefaultAsync(cancellationToken);

                var nomAuteur = auteur == null
                    ? "Quelqu'un"
                    : (string.IsNullOrWhiteSpace($"{auteur.Prenom} {auteur.Nom}".Trim())
                        ? (auteur.UserName ?? "Quelqu'un")
                        : $"{auteur.Prenom} {auteur.Nom}".Trim());

                var texte = string.Format(modele, nomAuteur, nomConversation);

                // La colonne fait 500 caractères : on borne le libellé de conversation.
                if (texte.Length > 500)
                    texte = texte[..499] + "…";

                _db.Notifications.Add(new Notification
                {
                    UtilisateurId = destinataireUserId,
                    Message = texte,
                    DateNotification = DateTime.Now,
                    EstLivree = false,
                    Type = type,
                    MessageDiscussionId = messageId
                });

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Best effort : une notification ratée ne doit jamais faire échouer
                // l'envoi du message de discussion.
                _logger.LogError(ex,
                    "Échec (ignoré) de la notification {Type} pour le message {MessageId}.", type, messageId);
            }
        }

        /// <summary>
        /// Écrit au plus DEUX notifications : l'attribution au nouveau responsable et le
        /// retrait à l'ancien. Ne lève jamais d'exception.
        /// </summary>
        private async Task ExecuterAsync(
            string? nouveauResponsableUserId,
            string? ancienResponsableUserId,
            string auteurUserId,
            TypeNotification type,
            TacheProduction tache,
            int? gmailMessageId,
            string message,
            string? messageRetrait,
            CancellationToken cancellationToken)
        {
            try
            {
                // Chaque message porte SON type : l'attribution et le retrait sont deux
                // événements distincts pour le lecteur (et pour la déduplication).
                var candidates = new List<(string destinataire, string texte, TypeNotification type)>();

                // Attribution — un responsable inchangé n'apprendrait rien de neuf.
                if (!string.IsNullOrEmpty(nouveauResponsableUserId)
                    && nouveauResponsableUserId != ancienResponsableUserId)
                {
                    candidates.Add((nouveauResponsableUserId, message, type));
                }

                // Retrait — uniquement si l'utilisateur était RÉELLEMENT responsable avant.
                if (messageRetrait != null
                    && !string.IsNullOrEmpty(ancienResponsableUserId)
                    && ancienResponsableUserId != nouveauResponsableUserId)
                {
                    candidates.Add((ancienResponsableUserId, messageRetrait, TypeNotification.TacheDesassignee));
                }

                foreach (var (destinataire, texte, typeNotification) in candidates)
                {
                    // Auto-notification inutile : l'auteur sait ce qu'il vient de faire.
                    if (destinataire == auteurUserId) continue;

                    // Défense en profondeur : l'appelant a déjà résolu un utilisateur actif,
                    // on ne se fie pas à ce contrat pour écrire une notification.
                    if (!await EstDestinataireValideAsync(destinataire, cancellationToken))
                    {
                        _logger.LogWarning(
                            "Notification ignorée : destinataire {Destinataire} inexistant ou inactif (tâche {TacheId}).",
                            destinataire, tache.Id);
                        continue;
                    }

                    // Déduplication : une même opération, appelée par plusieurs couches
                    // applicatives, ne doit pas produire deux notifications identiques.
                    if (await ExisteDejaAsync(destinataire, typeNotification, tache.Id))
                    {
                        _logger.LogDebug(
                            "Notification ignorée (déjà présente) : {Destinataire} / {Type} / tâche {TacheId}.",
                            destinataire, typeNotification, tache.Id);
                        continue;
                    }

                    _db.Notifications.Add(new Notification
                    {
                        UtilisateurId = destinataire,
                        Message = texte,
                        DateNotification = DateTime.Now,
                        EstLivree = false,
                        Type = typeNotification,
                        TacheProductionId = tache.Id,
                        GmailMessageId = gmailMessageId
                    });
                }

                if (_db.ChangeTracker.Entries<Notification>().Any(e => e.State == EntityState.Added))
                    await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Best effort (règle historique du planning) : notifier ne doit jamais
                // faire échouer l'assignation ni la création d'une tâche.
                _logger.LogError(ex,
                    "Échec (ignoré) de la notification {Type} pour la tâche {TacheId}.", type, tache.Id);
            }
        }

        private Task<bool> EstDestinataireValideAsync(string userId, CancellationToken cancellationToken) =>
            _db.Users.AnyAsync(u => u.Id == userId && u.EstActif, cancellationToken);

        private Task<bool> ExisteDejaAsync(string destinataire, TypeNotification type, int tacheId) =>
            _db.Notifications.AnyAsync(n =>
                n.UtilisateurId == destinataire
                && n.Type == type
                && n.TacheProductionId == tacheId
                && !n.EstLivree);

        /// <summary>Titre borné : le message doit tenir dans les 500 caractères de la colonne.</summary>
        private static string Titre(TacheProduction tache) =>
            string.IsNullOrWhiteSpace(tache.Titre)
                ? "(sans titre)"
                : (tache.Titre.Length <= LongueurMaxTitre
                    ? tache.Titre
                    : tache.Titre[..LongueurMaxTitre] + "…");
    }
}
