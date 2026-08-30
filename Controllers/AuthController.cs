using Backend_Gestion_Magasin_API.Dtos;
using Backend_Gestion_Magasin_API.Dtos.Auth;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Auth;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _context;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            TokenService tokenService,
            ILogger<AuthController> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _logger = logger;
            _context = context;
        }

        [HttpPost("register")]
        [Authorize]
        [RequireModulePermission("utilisateurs", requireWrite: true)]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("Un utilisateur avec cet email existe déjà.");

            var roleExiste = model.RoleId > 0 &&
                await _context.AppRoles.AnyAsync(r => r.Id == model.RoleId);
            if (model.RoleId > 0 && !roleExiste)
                return BadRequest("Le rôle sélectionné n'existe plus. Veuillez rafraîchir la page et réessayer.");

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Nom = model.Nom,
                Prenom = model.Prenom,
                RoleId = model.RoleId > 0 ? model.RoleId : null,
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Utilisateur enregistré avec succès.");
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                return Unauthorized();

            // Charger l'utilisateur avec son rôle personnalisé
            var userWithRole = await _userManager.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            var token = _tokenService.CreateToken(userWithRole ?? user);
            return Ok(new { token });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            return Ok(new {
                id = user.Id,
                email = user.Email,
                nom = user.Nom ?? user.UserName,
                prenom = user.Prenom ?? "",
                role = user.Role?.NomRole,
                roleId = user.RoleId,
                estAdministrateur = user.Role?.EstAdministrateur == true,
                estActif = user.EstActif
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // TODO: In a real application, send this token to the user's email.
                // For now, we log it to the console for development/testing purposes.
                _logger.LogWarning("Password reset token for {Email}: {Token}", user.Email, token);
            }

            // Return a generic success response to prevent email enumeration attacks.
            return Ok(new { message = "If an account with this email exists, a password reset link may have been sent." });
        }
    }
}
