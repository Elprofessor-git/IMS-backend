using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PermissionController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PermissionController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyPermissions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            // Administrateur → tout à true
            if (user.Role?.EstAdministrateur == true)
                return Ok(AllModules().Select(m =>
                    new { module = m, canAccess = true, canWrite = true }));

            // Pas de rôle → dashboard seulement
            if (user.Role == null)
                return Ok(new[]
                {
                    new { module = "dashboard", canAccess = true, canWrite = false }
                });

            // Rôle normal → mapping des booléens du rôle
            var role = user.Role;
            return Ok(new[]
            {
                Perm("articles",      role.PeutGererStock,        role.PeutValiderStock),
                Perm("stock",         role.PeutGererStock,        role.PeutValiderStock),
                Perm("mouvements",    role.PeutGererMouvements,   role.PeutGererMouvements),
                Perm("achats",        role.PeutGererAchats,       role.PeutConfirmerAchats),
                Perm("importations",  role.PeutGererImportations, role.PeutValiderImportations),
                Perm("commandes",     role.PeutGererCommandes,    role.PeutGererCommandes),
                Perm("clients",       role.PeutGererClients,      role.PeutGererClients),
                Perm("fournisseurs",  role.PeutGererFournisseurs, role.PeutGererFournisseurs),
                Perm("taches",        role.PeutGererTaches,       role.PeutGererTaches),
                Perm("utilisateurs",  role.PeutGererUtilisateurs, role.PeutGererUtilisateurs),
                Perm("roles",         role.EstAdministrateur,     role.EstAdministrateur),
                Perm("chatbot",       true,                       false),
                Perm("dashboard",     true,                       false),
            });
        }

        private static string[] AllModules() =>
        [
            "articles", "stock", "mouvements", "achats", "importations",
            "commandes", "clients", "fournisseurs", "taches",
            "utilisateurs", "roles", "chatbot", "dashboard"
        ];

        private static object Perm(string module, bool canAccess, bool canWrite) =>
            new { module, canAccess, canWrite };
    }
}
