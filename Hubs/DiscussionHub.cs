using Backend_Gestion_Magasin_API.Authentication;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Hubs
{
    /// <summary>
    /// Hub SignalR « Discussion » (lot temps réel) : améliore le polling sans le
    /// remplacer. Le polling (GET /api/Discussion/sync) reste le repli, notamment
    /// quand <c>NEXT_PUBLIC_DISCUSSION_HUB_URL</c> n'est pas défini côté client.
    ///
    /// Deux invariants de sécurité, non négociables :
    /// <list type="bullet">
    /// <item><b>Aucun groupe choisi par le client.</b> Le hub n'expose AUCUNE méthode
    /// cliente (pas de <c>RejoindreConversation</c>). À la connexion, le serveur déduit
    /// lui-même les groupes : le groupe personnel <c>utilisateur:{id}</c>, puis un groupe
    /// <c>conversation:{id}</c> par appartenance ACTIVE (ConversationMembre.AQuitteLe
    /// null). Un compte ne peut donc jamais s'abonner à une conversation dont il n'est
    /// pas membre.</item>
    /// <item><b>Authentification par ticket à usage unique</b>, jamais par JWT dans
    /// l'URL : voir <see cref="HubTicketAuthenticationHandler"/>.</item>
    /// </list>
    ///
    /// Les diffusions sont volontairement des « pokes » (un identifiant, pas de contenu) :
    /// le client invalide/recharge par l'API, qui applique les droits du LECTEUR
    /// (cartes de références E3) — un broadcast ne peut pas divulguer un message ou une
    /// référence qu'une réponse HTTP refuserait.
    /// </summary>
    [Authorize(AuthenticationSchemes = HubTicketAuthenticationHandler.SchemeName)]
    public sealed class DiscussionHub : Hub
    {
        private readonly ApplicationDbContext _db;

        public DiscussionHub(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>Groupe personnel : notifications et rafraîchissement de la cloche.</summary>
        public static string GroupeUtilisateur(string userId) => $"utilisateur:{userId}";

        /// <summary>Groupe d'une conversation : messages et mutations.</summary>
        public static string GroupeConversation(int conversationId) => $"conversation:{conversationId}";

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupeUtilisateur(userId));

                // Groupes DÉDUITS des appartenances actives : le client n'en demande aucun.
                var conversations = await _db.ConversationMembres
                    .AsNoTracking()
                    .Where(m => m.UserId == userId && m.AQuitteLe == null)
                    .Select(m => m.ConversationId)
                    .ToListAsync(Context.ConnectionAborted);

                foreach (var conversationId in conversations)
                    await Groups.AddToGroupAsync(Context.ConnectionId, GroupeConversation(conversationId));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // SignalR retire automatiquement toutes les appartenances de groupe à la
            // déconnexion ; on explicite seulement le groupe personnel pour la lisibilité.
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupeUtilisateur(userId));

            await base.OnDisconnectedAsync(exception);
        }
    }
}
