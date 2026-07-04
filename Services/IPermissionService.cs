namespace Backend_Gestion_Magasin_API.Services
{
    public record ModulePermission(string Module, bool CanAccess, bool CanWrite);

    public interface IPermissionService
    {
        Task<(bool canAccess, bool canWrite)> GetPermissionAsync(string userId, string module);
        Task<IEnumerable<ModulePermission>> GetAllPermissionsAsync(string userId);
    }
}
