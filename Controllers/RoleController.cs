using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/roles")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RoleController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles()
        {
            var roles = await _roleManager.Roles
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name ?? ""
                })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole(CreateRoleDto createRoleDto)
        {
            if (string.IsNullOrWhiteSpace(createRoleDto.Name))
                return BadRequest("Le nom du rôle est obligatoire.");

            var roleExists = await _roleManager.RoleExistsAsync(createRoleDto.Name);
            if (roleExists)
                return BadRequest("Ce rôle existe déjà.");

            var result = await _roleManager.CreateAsync(new IdentityRole(createRoleDto.Name));
            if (result.Succeeded)
                return Ok(new { message = "Rôle créé avec succès" });

            return BadRequest(result.Errors);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound();

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
                return NoContent();

            return BadRequest(result.Errors);
        }
    }
}
