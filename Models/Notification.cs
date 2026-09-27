using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend_Gestion_Magasin_API.Models.Gmail;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Origine d'une notification. Sert à l'affichage (icône/libellé dans la cloche) et à la
    /// navigation au clic (deep-link) ; il ne remplace PAS les FK nullable qui restent la
    /// source de vérité de la cible.
    ///
    /// Convention du projet : noms français, comme <see cref="StatutTache"/>. Les valeurs
    /// sont stockées en texte (conversion string du DbContext) pour rester lisibles en base
    /// et insensibles à un réordonnancement ultérieur de l'énumération.
    /// </summary>
    public enum TypeNotification
    {
        /// <summary>Changement du planning de production (comportement historique, inchangé).</summary>
        Planning = 0,

        /// <summary>Une tâche dont je suis responsable vient de m'être confiée.</summary>
        TacheAssignee = 1,

        /// <summary>Une tâche dont j'étais responsable ne m'est plus confiée.</summary>
        TacheDesassignee = 2,

        /// <summary>Une tâche a été créée depuis un email et m'est confiée.</summary>
        TacheDepuisEmail = 3
    }

    /// <summary>
    /// Notification d'un utilisateur : « Le planning a changé : cellule X pour le
    /// samedi Y, commande Z ». Chaque utilisateur reçoit sa propre ligne de
    /// notification et peut la marquer livrée (lue) individuellement.
    ///
    /// Une notification appartient EXCLUSIVEMENT à son destinataire
    /// (<see cref="UtilisateurId"/>) : elle n'est ni consultable, ni marquable, ni
    /// supprimable par un autre utilisateur, quelle que soit la ressource liée.
    /// </summary>
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UtilisateurId { get; set; } = string.Empty;

        /// <summary>Message textuel de la notification (ex. « Le planning a changé : cellule Découpe pour le 21/09/2026, commande 79-PO33341 »).</summary>
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public DateTime DateNotification { get; set; } = DateTime.Now;

        /// <summary>L'utilisateur a-t-il marqué cette notification comme livrée (lue) ?</summary>
        public bool EstLivree { get; set; }

        /// <summary>Origine de l'événement. Le planning reste la valeur par défaut (données existantes).</summary>
        public TypeNotification Type { get; set; } = TypeNotification.Planning;

        // ── Cibles du lien profond (au plus une renseignée, cohérente avec Type) ──
        // Nullable + SetNull : la notification survit à la suppression de sa cible, et le
        // message reste lisible. Ces colonnes ne sont JAMAIS utilisées pour autoriser un
        // accès : seul l'endpoint d'origine (Tâches / Courriels) applique l'ownership.

        /// <summary>Entrée de planning liée (optionnel).</summary>
        public int? PlanningEntryId { get; set; }

        /// <summary>Tâche liée (optionnel) : assignation, désassignation, tâche issue d'un email.</summary>
        public int? TacheProductionId { get; set; }

        /// <summary>Email lié (optionnel) : conservé pour les tâches nées d'un email, afin de
        /// pouvoir revenir au courrier d'origine.</summary>
        public int? GmailMessageId { get; set; }

        // Relations
        public virtual PlanningEntry? PlanningEntry { get; set; }
        public virtual TacheProduction? TacheProduction { get; set; }
        public virtual GmailMessage? GmailMessage { get; set; }
    }
}
