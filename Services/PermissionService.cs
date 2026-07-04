using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _db;

        public PermissionService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<(bool canAccess, bool canWrite)> GetPermissionAsync(string userId, string module)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return (false, false);
            if (user.Role?.EstAdministrateur == true) return (true, true);
            if (user.Role == null) return (module is "dashboard" or "chatbot", false);

            return MapModule(user.Role, module);
        }

        public async Task<IEnumerable<ModulePermission>> GetAllPermissionsAsync(string userId)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return [];

            if (user.Role?.EstAdministrateur == true)
                return AllModules().Select(m => new ModulePermission(m, true, true));

            if (user.Role == null)
                return [new ModulePermission("dashboard", true, false)];

            return AllModules().Select(m =>
            {
                var (canAccess, canWrite) = MapModule(user.Role, m);
                return new ModulePermission(m, canAccess, canWrite);
            });
        }

        private static (bool canAccess, bool canWrite) MapModule(Role role, string module) =>
            module switch
            {
                "articles"     => (role.PeutGererStock,        role.PeutValiderStock),
                "stock"        => (role.PeutGererStock,        role.PeutValiderStock),
                "mouvements"   => (role.PeutGererMouvements,   role.PeutGererMouvements),
                "achats"       => (role.PeutGererAchats,       role.PeutConfirmerAchats),
                "importations" => (role.PeutGererImportations, role.PeutValiderImportations),
                "commandes"    => (role.PeutGererCommandes,    role.PeutGererCommandes),
                "clients"      => (role.PeutGererClients,      role.PeutGererClients),
                "fournisseurs" => (role.PeutGererFournisseurs, role.PeutGererFournisseurs),
                "plateformes"  => (role.PeutGererClients,      role.PeutGererClients),
                "taches"       => (role.PeutGererTaches,       role.PeutGererTaches),
                "utilisateurs" => (role.PeutGererUtilisateurs, role.PeutGererUtilisateurs),
                "roles"        => (role.EstAdministrateur,     role.EstAdministrateur),
                "chatbot"      => (true,                       false),
                "dashboard"    => (true,                       false),
                "rapports"     => (true,                       false),
                _              => (false,                      false),
            };

        private static string[] AllModules() =>
        [
            "articles", "stock", "mouvements", "achats", "importations",
            "commandes", "clients", "fournisseurs", "plateformes", "taches",
            "utilisateurs", "roles", "chatbot", "dashboard", "rapports"
        ];
    }
}
