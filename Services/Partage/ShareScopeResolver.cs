using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Partage
{
    /// <summary>
    /// Périmètre résolu d'un lien : les ensembles d'identifiants auxquels chaque
    /// section doit se restreindre.
    /// </summary>
    /// <remarks>
    /// Les trois ensembles ne sont PAS les « données de la plateforme » : ils
    /// décrivent les propriétaires DIRECTS autorisés, du niveau du lien et EN
    /// DESSOUS. Un lien de portée Marque ne met donc pas PlateformeIds à la
    /// plateforme parente — sinon le stock rattaché directement à la plateforme
    /// (hors de la marque) fuiterait dans un lien de marque.
    ///
    /// Séparation voulue :
    ///   Plateforme → PlateformeIds={X}, ClientIds=marques(X), CommandeIds=commandes(X)
    ///   Marque     → PlateformeIds={},  ClientIds={X},       CommandeIds=commandes(X)
    ///   Commande   → PlateformeIds={},  ClientIds={},        CommandeIds={X}
    /// </remarks>
    public sealed record PartageScope(
        ShareLinkScopeType Type,
        int ScopeId,
        string Libelle,
        IReadOnlySet<int> PlateformeIds,
        IReadOnlySet<int> ClientIds,
        IReadOnlySet<int> CommandeIds,
        IReadOnlySet<int> GroupeIds);

    public sealed class ShareScopeResolver
    {
        private readonly ApplicationDbContext _db;

        public ShareScopeResolver(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Résout le périmètre. Retour null si l'entité de portée n'existe pas (ou
        /// n'est plus) : le lien est alors traité comme invalide, pas comme vide.
        /// </summary>
        public async Task<PartageScope?> ResoudreAsync(
            ShareLinkScopeType type, int scopeId, CancellationToken ct)
        {
            switch (type)
            {
                case ShareLinkScopeType.Plateforme:
                {
                    var plateforme = await _db.Plateformes.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == scopeId, ct);
                    if (plateforme == null) return null;

                    var clients = await _db.Clients.AsNoTracking()
                        .Where(c => c.PlateformeId == scopeId)
                        .Select(c => c.Id)
                        .ToListAsync(ct);

                    var commandes = await _db.CommandesClients.AsNoTracking()
                        .Where(c => c.Client != null && c.Client.PlateformeId == scopeId)
                        .Select(c => c.Id)
                        .ToListAsync(ct);

                    return new PartageScope(
                        type, scopeId, plateforme.Nom,
                        new HashSet<int> { scopeId },
                        clients.ToHashSet(),
                        commandes.ToHashSet(),
                        await GroupesEntierementDansAsync(commandes, ct));
                }

                case ShareLinkScopeType.Marque:
                {
                    var client = await _db.Clients.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == scopeId, ct);
                    if (client == null) return null;

                    var commandes = await _db.CommandesClients.AsNoTracking()
                        .Where(c => c.ClientId == scopeId)
                        .Select(c => c.Id)
                        .ToListAsync(ct);

                    // PlateformeIds reste vide : le stock rattaché directement à la
                    // plateforme parente n'appartient pas à la marque.
                    return new PartageScope(
                        type, scopeId, client.Nom,
                        new HashSet<int>(),
                        new HashSet<int> { scopeId },
                        commandes.ToHashSet(),
                        await GroupesEntierementDansAsync(commandes, ct));
                }

                case ShareLinkScopeType.Commande:
                {
                    var commande = await _db.CommandesClients.AsNoTracking()
                        .Include(c => c.Client)
                        .FirstOrDefaultAsync(c => c.Id == scopeId, ct);
                    if (commande == null || commande.Client == null) return null;

                    var commandeIds = new List<int> { scopeId };
                    return new PartageScope(
                        type, scopeId, commande.NumeroCommande,
                        new HashSet<int>(),
                        new HashSet<int>(),
                        commandeIds.ToHashSet(),
                        await GroupesEntierementDansAsync(commandeIds, ct));
                }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Groupes dont TOUTES les commandes membres appartiennent au périmètre.
        /// </summary>
        /// <remarks>
        /// Décision métier : un groupe n'est inclus que s'il est ENTIÈREMENT dans le
        /// périmètre. Un groupe à cheval (une commande dedans, une dehors) est exclu,
        /// car une ligne ou un stock rattaché au groupe révélerait l'existence et le
        /// contenu de commandes hors périmètre. Exclusion > fuite : c'est le seul
        /// arbitrage acceptable pour un lien qu'on ne peut pas révoquer rétroactivement.
        /// </remarks>
        private async Task<HashSet<int>> GroupesEntierementDansAsync(
            IReadOnlyCollection<int> commandeIds, CancellationToken ct)
        {
            if (commandeIds.Count == 0) return new HashSet<int>();

            // « au moins un membre » empêche qu'un groupe vide satisfasse trivialement
            // la condition, et « aucun membre hors ensemble » exprime le « tous ».
            var ids = await _db.GroupesCommandes.AsNoTracking()
                .Where(g => g.Membres.Any()
                         && !g.Membres.Any(m => !commandeIds.Contains(m.CommandeClientId)))
                .Select(g => g.Id)
                .ToListAsync(ct);

            return ids.ToHashSet();
        }
    }
}