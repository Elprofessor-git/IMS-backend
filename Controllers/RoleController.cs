using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/roles")]
    [ApiController]
    [Authorize(Roles = "Admin")]
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
            // On récupère les données depuis votre table personnalisée
            var roles = await _context.AppRoles.ToListAsync();
            
            var result = roles.Select(r => new RoleDto
            {
                Id = r.Id.ToString(), // Conversion explicite pour satisfaire le compilateur et Angular
                Name = r.NomRole,
                Description = r.Description,
                PeutGererStock = r.PeutGererStock,
                EstAdministrateur = r.EstAdministrateur
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleDto createRoleDto)
        {
            Console.WriteLine($"===> [DB-DEBUG] Requête POST reçue pour le rôle : {createRoleDto.Name}");
            var role = new Role
            {
                NomRole = createRoleDto.Name,
                Description = createRoleDto.Description,
                PeutGererStock = createRoleDto.PeutGererStock,
                EstAdministrateur = createRoleDto.EstAdministrateur,
                DateCreation = DateTime.Now,
                EstActif = true
            };

            _context.AppRoles.Add(role);
            await _context.SaveChangesAsync();
            Console.WriteLine($"===> [DB-DEBUG] Rôle enregistré avec succès ! ID généré : {role.Id}");

            return Ok(new RoleDto
            {
                Id = role.Id.ToString(),
                Name = role.NomRole,
                Description = role.Description,
                PeutGererStock = role.PeutGererStock,
                EstAdministrateur = role.EstAdministrateur
            });
        }

        // PUT: api/roles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, CreateRoleDto model)
        {
            var role = await _context.AppRoles.FindAsync(id);
            if (role == null) return NotFound();

            role.NomRole = model.Name;
            role.Description = model.Description;
            role.PeutGererStock = model.PeutGererStock;
            role.EstAdministrateur = model.EstAdministrateur;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/roles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.AppRoles.FindAsync(id);
            if (role == null) return NotFound();

            _context.AppRoles.Remove(role);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
