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
    [AllowAnonymous]
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

        // POST: api/roles
        [HttpPost]
        public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleDto createRoleDto)
        {
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

            return Ok(new RoleDto
            {
                Id = role.Id.ToString(),
                Name = role.NomRole,
                Description = role.Description
            });
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
