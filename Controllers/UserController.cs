using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/Account/users")]
    [ApiController]
    [Authorize]
    [RequireModulePermission("utilisateurs")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UserController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<ReadUserDto>>> GetAll()
        {
            var users = await _userManager.Users.Include(u => u.Role).ToListAsync();
            var userDtos = new List<ReadUserDto>();

            foreach (var user in users)
            {
                userDtos.Add(new ReadUserDto
                {
                    Id = user.Id,
                    Nom = user.Nom,
                    Prenom = user.Prenom,
                    Email = user.Email ?? "",
                    RoleId = user.RoleId,
                    NomRole = user.Role?.NomRole,
                    EstActif = user.EstActif,
                    DateCreation = user.DateCreation
                });
            }

            return Ok(userDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ReadUserDto>> GetById(string id)
        {
            var user = await _userManager.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            return Ok(new ReadUserDto
            {
                Id = user.Id,
                Nom = user.Nom,
                Prenom = user.Prenom,
                Email = user.Email ?? "",
                RoleId = user.RoleId,
                NomRole = user.Role?.NomRole,
                EstActif = user.EstActif,
                DateCreation = user.DateCreation
            });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("utilisateurs", requireWrite: true)]
        public async Task<IActionResult> Update(string id, UpdateUserDto updateDto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            // Garde-fou : seul un administrateur peut changer le rôle d'un utilisateur
            // (y compris le sien). Un non-administrateur ne peut ni s'attribuer le rôle
            // administrateur, ni l'assigner à quelqu'un d'autre, ni modifier un rôle.
            var userId = _userManager.GetUserId(User);
            if (userId == null)
                return Unauthorized();

            var me = await _userManager.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
            var jeSuisAdministrateur = me?.Role?.EstAdministrateur ?? false;

            if (!jeSuisAdministrateur && updateDto.RoleId.HasValue)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Réservé aux administrateurs : la modification du rôle d'un utilisateur nécessite le rôle Administrateur." });

            // Garde-fou : un administrateur ne peut pas retirer son propre statut
            // administrateur (changement de rôle sur son propre compte).
            if (userId == id && updateDto.RoleId.HasValue && jeSuisAdministrateur)
            {
                var roleCible = updateDto.RoleId.Value > 0
                    ? await _context.AppRoles.FindAsync(updateDto.RoleId.Value)
                    : null;
                var cibleEstAdministrateur = roleCible?.EstAdministrateur ?? false;
                if (!cibleEstAdministrateur)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Impossible de retirer votre propre statut administrateur." });
            }

            if (!string.IsNullOrEmpty(updateDto.Nom))
                user.Nom = updateDto.Nom;

            if (!string.IsNullOrEmpty(updateDto.Prenom))
                user.Prenom = updateDto.Prenom;

            if (!string.IsNullOrEmpty(updateDto.Email))
                user.Email = updateDto.Email;

            if (!string.IsNullOrEmpty(updateDto.Poste))
                user.Poste = updateDto.Poste;

            if (!string.IsNullOrEmpty(updateDto.Equipe))
                user.Equipe = updateDto.Equipe;

            if (!string.IsNullOrEmpty(updateDto.Departement))
                user.Departement = updateDto.Departement;

            if (updateDto.EstActif.HasValue)
                user.EstActif = updateDto.EstActif.Value;

            // RoleId : 0 = retirer le rôle, valeur positive = assigner, absent/null = inchangé
            if (updateDto.RoleId.HasValue)
                user.RoleId = updateDto.RoleId.Value == 0 ? null : updateDto.RoleId;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("utilisateurs", requireWrite: true)]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }
    }
}
