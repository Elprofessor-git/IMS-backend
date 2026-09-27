namespace Backend_Gestion_Magasin_API.Services
{
    public record ModulePermission(string Module, bool CanAccess, bool CanWrite);

    public interface IPermissionService
    {
        Task<(bool canAccess, bool canWrite)> GetPermissionAsync(string userId, string module);
        Task<IEnumerable<ModulePermission>> GetAllPermissionsAsync(string userId);

        /// <summary>
        /// Droit de RESSOURCE sur le module Tâches : autorise « scope=all » et l'accès
        /// aux tâches dont l'utilisateur n'est ni le créateur ni le responsable.
        /// Distinct de la permission de module GetPermissionAsync(userId, "taches").
        /// </summary>
        Task<bool> CanViewAllTachesAsync(string userId);

        /// <summary>
        /// Droit d'affecter une tâche à un autre utilisateur IMS.
        /// </summary>
        Task<bool> CanAssignerTachesAsync(string userId);
    }
}
