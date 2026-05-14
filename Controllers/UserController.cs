using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Dtos;

namespace Backend_Gestion_Magasin_API.Controllers
{
[Route("api/Account/users")]
[ApiController]
[Authorize(Roles = "Admin")]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

        public UserController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
        public async Task<ActionResult<List<ReadUserDto>>> GetAll()
        {
            var users = _userManager.Users.ToList();
            var userDtos = new List<ReadUserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new ReadUserDto
                {
                    Id = user.Id,
                    Nom = user.Nom,
                    Prenom = user.Prenom,
                    Email = user.Email ?? "",
                    Role = roles.FirstOrDefault(),
                    EstActif = user.EstActif,
                    DateCreation = user.DateCreation
                });
            }

            return Ok(userDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ReadUserDto>> GetById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
            var userDto = new ReadUserDto
            {
                Id = user.Id,
                Nom = user.Nom,
                Prenom = user.Prenom,
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault(),
                EstActif = user.EstActif,
                DateCreation = user.DateCreation
            };

            return Ok(userDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, UpdateUserDto updateDto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

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

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }

        [HttpDelete("{id}")]
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
