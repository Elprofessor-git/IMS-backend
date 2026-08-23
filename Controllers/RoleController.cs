using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/roles")]
    [ApiController]
    [Authorize]
    [RequireModulePermission("roles")]
    public class RoleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RoleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/roles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles()
        {
            var roles = await _context.AppRoles.ToListAsync();
            return Ok(roles.Select(MapToDto));
        }

        // GET: api/roles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RoleDto>> GetRole(int id)
        {
            var role = await _context.AppRoles.FindAsync(id);
            if (role == null) return NotFound();
            return Ok(MapToDto(role));
        }

        // POST: api/roles
        [HttpPost]
        [RequireModulePermission("roles", requireWrite: true)]
        public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleDto dto)
        {
            var role = new Role
            {
                NomRole = dto.Name,
                Description = dto.Description,
                EstAdministrateur = dto.EstAdministrateur,
                PeutGererStock = dto.PeutGererStock,
                PeutGererCommandes = dto.PeutGererCommandes,
                PeutGererTaches = dto.PeutGererTaches,
                PeutGererClients = dto.PeutGererClients,
                PeutGererFournisseurs = dto.PeutGererFournisseurs,
                PeutGererAchats = dto.PeutGererAchats,
                PeutGererImportations = dto.PeutGererImportations,
                PeutGererUtilisateurs = dto.PeutGererUtilisateurs,
                PeutGererMouvements = dto.PeutGererMouvements,
                PeutGererPlateformes = dto.PeutGererPlateformes,
                PeutVoirMouvements = dto.PeutVoirMouvements,
                PeutVoirCommandes = dto.PeutVoirCommandes,
                PeutVoirClients = dto.PeutVoirClients,
                PeutVoirFournisseurs = dto.PeutVoirFournisseurs,
                PeutVoirPlateformes = dto.PeutVoirPlateformes,
                PeutVoirTaches = dto.PeutVoirTaches,
                PeutVoirUtilisateurs = dto.PeutVoirUtilisateurs,
                PeutVoirRoles = dto.PeutVoirRoles,
                PeutValiderStock = dto.PeutValiderStock,
                PeutConfirmerAchats = dto.PeutConfirmerAchats,
                PeutValiderImportations = dto.PeutValiderImportations,
                PeutVoirDashboard = dto.PeutVoirDashboard,
                PeutVoirRapports = dto.PeutVoirRapports,
                DateCreation = DateTime.Now,
                EstActif = true
            };

            _context.AppRoles.Add(role);
            await _context.SaveChangesAsync();
            return Ok(MapToDto(role));
        }

        // PUT: api/roles/5
        [HttpPut("{id}")]
        [RequireModulePermission("roles", requireWrite: true)]
        public async Task<IActionResult> UpdateRole(int id, CreateRoleDto dto)
        {
            var role = await _context.AppRoles.FindAsync(id);
            if (role == null) return NotFound();

            role.NomRole = dto.Name;
            role.Description = dto.Description;
            role.EstAdministrateur = dto.EstAdministrateur;
            role.PeutGererStock = dto.PeutGererStock;
            role.PeutGererCommandes = dto.PeutGererCommandes;
            role.PeutGererTaches = dto.PeutGererTaches;
            role.PeutGererClients = dto.PeutGererClients;
            role.PeutGererFournisseurs = dto.PeutGererFournisseurs;
            role.PeutGererAchats = dto.PeutGererAchats;
            role.PeutGererImportations = dto.PeutGererImportations;
            role.PeutGererUtilisateurs = dto.PeutGererUtilisateurs;
            role.PeutGererMouvements = dto.PeutGererMouvements;
            role.PeutGererPlateformes = dto.PeutGererPlateformes;
            role.PeutVoirMouvements = dto.PeutVoirMouvements;
            role.PeutVoirCommandes = dto.PeutVoirCommandes;
            role.PeutVoirClients = dto.PeutVoirClients;
            role.PeutVoirFournisseurs = dto.PeutVoirFournisseurs;
            role.PeutVoirPlateformes = dto.PeutVoirPlateformes;
            role.PeutVoirTaches = dto.PeutVoirTaches;
            role.PeutVoirUtilisateurs = dto.PeutVoirUtilisateurs;
            role.PeutVoirRoles = dto.PeutVoirRoles;
            role.PeutValiderStock = dto.PeutValiderStock;
            role.PeutConfirmerAchats = dto.PeutConfirmerAchats;
            role.PeutValiderImportations = dto.PeutValiderImportations;
            role.PeutVoirDashboard = dto.PeutVoirDashboard;
            role.PeutVoirRapports = dto.PeutVoirRapports;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/roles/5
        [HttpDelete("{id}")]
        [RequireModulePermission("roles", requireWrite: true)]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.AppRoles.FindAsync(id);
            if (role == null) return NotFound();

            _context.AppRoles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static RoleDto MapToDto(Role r) => new()
        {
            Id = r.Id,
            Name = r.NomRole,
            Description = r.Description,
            EstActif = r.EstActif,
            EstAdministrateur = r.EstAdministrateur,
            PeutGererStock = r.PeutGererStock,
            PeutGererCommandes = r.PeutGererCommandes,
            PeutGererTaches = r.PeutGererTaches,
            PeutGererClients = r.PeutGererClients,
            PeutGererFournisseurs = r.PeutGererFournisseurs,
            PeutGererAchats = r.PeutGererAchats,
            PeutGererImportations = r.PeutGererImportations,
            PeutGererUtilisateurs = r.PeutGererUtilisateurs,
            PeutGererMouvements = r.PeutGererMouvements,
            PeutGererPlateformes = r.PeutGererPlateformes,
            PeutVoirMouvements = r.PeutVoirMouvements,
            PeutVoirCommandes = r.PeutVoirCommandes,
            PeutVoirClients = r.PeutVoirClients,
            PeutVoirFournisseurs = r.PeutVoirFournisseurs,
            PeutVoirPlateformes = r.PeutVoirPlateformes,
            PeutVoirTaches = r.PeutVoirTaches,
            PeutVoirUtilisateurs = r.PeutVoirUtilisateurs,
            PeutVoirRoles = r.PeutVoirRoles,
            PeutValiderStock = r.PeutValiderStock,
            PeutConfirmerAchats = r.PeutConfirmerAchats,
            PeutValiderImportations = r.PeutValiderImportations,
            PeutVoirDashboard = r.PeutVoirDashboard,
            PeutVoirRapports = r.PeutVoirRapports,
        };
    }
}
