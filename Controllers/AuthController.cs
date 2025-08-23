using Backend_Gestion_Magasin_API.Dtos;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Models.Auth;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Gestion_Magasin_API.Controllers
{
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokenService;
    private readonly RoleManager<IdentityRole> _roleManager;

        public AuthController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, 
            TokenService tokenService,
            RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _roleManager = roleManager;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
                return BadRequest("Un utilisateur avec cet email existe déjà.");

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            Nom = model.Nom
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

            // Vérifie si le rôle existe, sinon le crée
        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            await _roleManager.CreateAsync(new IdentityRole(model.Role));
        }

            // Ajoute le rôle à l'utilisateur
        await _userManager.AddToRoleAsync(user, model.Role);

            return Ok("Utilisateur enregistré avec succès.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            return Unauthorized();

        var token = _tokenService.CreateToken(user);
        return Ok(new { token });
        }
    }
}


