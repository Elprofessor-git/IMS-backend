using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    public interface ITacheOwnershipService
    {
        /// <summary>
        /// Droit de ressource : l'utilisateur peut voir et agir sur les tâches des autres
        /// (rôle administrateur, ou rôle portant explicitement PeutVoirToutesTaches).
        /// </summary>
        Task<bool> CanAccessAllAsync(string userId);

        /// <summary>
        /// Restreint une requête aux tâches accessibles à l'utilisateur. Avec
        /// <paramref name="canAccessAll"/> à true, la requête n'est pas filtrée.
        /// </summary>
        IQueryable<TacheProduction> ApplyVisibility(
            IQueryable<TacheProduction> query,
            string userId,
            bool canAccessAll);

        /// <summary>
        /// Charge une tâche par identifiant SI l'utilisateur y a droit, sinon null.
        /// null se traduit par un 404 côté contrôleur : l'existence d'une tâche qui
        /// appartient à autrui n'est jamais divulguée (même convention que le module
        /// Courriels, qui répond 404 uniformément).
        /// </summary>
        Task<TacheProduction?> FindVisibleAsync(int id, string userId, bool canAccessAll);

        /// <summary>
        /// Variante TRACKÉE, pour les routes qui écrivent ensuite via SaveChangesAsync.
        /// Le filtre de visibilité est strictement identique à <see cref="FindVisibleAsync"/> :
        /// c'est le seul point d'application de la règle, il ne doit pas être dupliqué.
        /// </summary>
        Task<TacheProduction?> FindVisibleForWriteAsync(int id, string userId, bool canAccessAll);

        /// <summary>Destinataire d'assignation valide (existant ET actif), sinon null.</summary>
        Task<ApplicationUser?> FindActiveAssigneeAsync(string assigneeUserId);

        /// <summary>Libellé lisible « Prénom Nom », ou le nom seul.</summary>
        string? DisplayNameOf(ApplicationUser? user);

        /// <summary>
        /// Affecte le responsable et resynchronise le champ legacy ResponsableAssigne,
        /// afin qu'il n'existe jamais deux sources de vérité contradictoires.
        /// </summary>
        void SetAssignee(TacheProduction tache, ApplicationUser? assignee);
    }

    public class TacheOwnershipService : ITacheOwnershipService
    {
        private readonly ApplicationDbContext _db;
        private readonly IPermissionService _permissions;

        public TacheOwnershipService(ApplicationDbContext db, IPermissionService permissions)
        {
            _db = db;
            _permissions = permissions;
        }

        public Task<bool> CanAccessAllAsync(string userId) =>
            _permissions.CanViewAllTachesAsync(userId);

        public IQueryable<TacheProduction> ApplyVisibility(
            IQueryable<TacheProduction> query,
            string userId,
            bool canAccessAll)
        {
            if (canAccessAll)
                return query;

            // « Mes tâches » : les tâches dont je suis le créateur OU le responsable.
            // Les tâches historiques sans propriétaire (les deux FK NULL) restent
            // hors périmètre d'un utilisateur ordinaire : elles ne sont visibles que
            // des rôles disposant de PeutVoirToutesTaches.
            return query.Where(t => t.CreatedByUserId == userId || t.AssignedToUserId == userId);
        }

        public async Task<TacheProduction?> FindVisibleAsync(int id, string userId, bool canAccessAll)
        {
            var query = ApplyVisibility(_db.TachesProduction.AsNoTracking(), userId, canAccessAll);

            return await query.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TacheProduction?> FindVisibleForWriteAsync(int id, string userId, bool canAccessAll)
        {
            // Pas d'AsNoTracking : l'entité retournée doit être suivie pour que
            // SaveChangesAsync persiste les modifications.
            var query = ApplyVisibility(_db.TachesProduction, userId, canAccessAll);

            return await query.FirstOrDefaultAsync(t => t.Id == id);
        }

        public Task<ApplicationUser?> FindActiveAssigneeAsync(string assigneeUserId) =>
            _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == assigneeUserId && u.EstActif);

        public string? DisplayNameOf(ApplicationUser? user)
        {
            if (user == null)
                return null;

            return string.IsNullOrWhiteSpace(user.Prenom) ? user.Nom : $"{user.Prenom} {user.Nom}";
        }

        public void SetAssignee(TacheProduction tache, ApplicationUser? assignee)
        {
            tache.AssignedToUserId = assignee?.Id;

            // Champ legacy aligné sur la FK (source de vérité), jamais l'inverse.
            tache.ResponsableAssigne = assignee == null
                ? null
                : DisplayNameOf(assignee);
        }
    }
}
