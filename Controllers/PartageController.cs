using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Partage;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Services.Partage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Liens de partage en lecture seule. Le préfixe anonyme est UNIQUE dans tout le
    /// contrôleur (<see cref="Public"/>) : c'est la seule porte d'entrée sans session,
    /// et elle ne rend que des projections filtrées.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PartageController : ControllerBase
    {
        private readonly IShareLinkService _service;
        private readonly IPermissionService _permissions;
        private readonly ApplicationDbContext _db;

        public PartageController(
            IShareLinkService service,
            IPermissionService permissions,
            ApplicationDbContext db)
        {
            _service = service;
            _permissions = permissions;
            _db = db;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST api/partage — créer un lien
        // ─────────────────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Creer([FromBody] CreateShareLinkDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (!await _permissions.CanPartagerLiensAsync(userId))
                return RefusPartage();

            if (dto.ScopeId <= 0)
                return BadRequest(new { message = "Portée invalide." });
            if (dto.Sections == ShareLinkSection.Aucune)
                return BadRequest(new { message = "Sélectionnez au moins une section à partager." });
            if (dto.MaxUses is <= 0)
                return BadRequest(new { message = "Le nombre d'ouvertures doit être positif." });

            var resultat = await _service.CreerAsync(dto, userId, HttpContext.RequestAborted);
            if (resultat == null)
                return BadRequest(new { message = "Portée introuvable." });

            return StatusCode(StatusCodes.Status201Created, resultat);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET api/partage — liste de SES liens (l'admin voit tout)
        // ─────────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Lister()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (!await _permissions.CanPartagerLiensAsync(userId))
                return RefusPartage();

            var estAdmin = await EstAdministrateurAsync(userId);
            var liens = await _service.ListerAsync(userId, estAdmin, HttpContext.RequestAborted);
            return Ok(liens);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // DELETE api/partage/{id} — révoquer
        // ─────────────────────────────────────────────────────────────────────────

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Revoquer(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            // Un porteur dont le droit a été révoqué ne peut plus révoquer ses propres
            // liens, SAUF administrateur : c'est justement le cas « départ d'un employé »
            // où il faut pouvoir couper un lien laissé actif.
            var estAdmin = await EstAdministrateurAsync(userId);
            if (!estAdmin && !await _permissions.CanPartagerLiensAsync(userId))
                return RefusPartage();

            var ok = await _service.RevoquerAsync(id, userId, estAdmin, HttpContext.RequestAborted);

            // 404 identique pour « inconnu » et « pas à moi » : ne pas distinguer évite
            // de confirmer l'existence d'un lien d'autrui.
            return ok ? NoContent() : NotFound();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST api/partage/public — ouvrir un lien (ANONYME, limité par IP)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Le token voyage dans le CORPS, jamais dans l'URL : une URL finit dans les
        /// journaux d'accès (Vercel/Render, proxies, historiques) alors qu'un corps de
        /// requête POST non journalisé n'y apparaît pas.
        /// </summary>
        [HttpPost("public")]
        [AllowAnonymous]
        [EnableRateLimiting("partage-public")]
        public async Task<IActionResult> Public([FromBody] PublicShareRequestDto dto)
        {
            // Posés AVANT toute sortie, y compris 404 : une réponse d'erreur mise en
            // cache par un intermédiaire pourrait figer un 404 sur un lien ensuite
            // valide, et un navigateur ne doit jamais transmettre ce chemin en Referer.
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";
            Response.Headers.Pragma = "no-cache";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var donnees = await _service.OuvrirAsync(dto.Token, ip, HttpContext.RequestAborted);

            // 404 unique : token inconnu, expiré, révoqué ou saturé sont indiscernables.
            if (donnees == null) return NotFound();
            return Ok(donnees);
        }

        // ─────────────────────────────────────────────────────────────────────────

        private static ObjectResult RefusPartage() => new(new
        {
            message = "Vous n'avez pas le droit de partager des liens. Demandez à un administrateur de vous l'accorder."
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };

        private async Task<bool> EstAdministrateurAsync(string userId)
        {
            var role = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Role != null && u.Role.EstAdministrateur)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            return role;
        }
    }
}