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
        /// <summary>
        /// Plafond d'articles sélectionnables dans un lien. Au-delà, le lien
        /// devient un export déguisé et la sélection perd son sens ; la borne est
        /// donc refusée à la création plutôt que tronquée en silence.
        /// </summary>
        private const int MaxArticlesPartages = 200;

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

            // Règle A : partager du STOCK demande le droit stock, en plus du droit
            // de partager. Sans cela, un rôle « lecture seule » verrait partir des
            // liens exposant des quantités qu'il ne peut pas gérer.
            if ((dto.Sections & ShareLinkSection.Stock) != 0)
            {
                var (peutAccederStock, _) =
                    await _permissions.GetPermissionAsync(userId, "stock");
                if (!peutAccederStock) return RefusStock();
            }

            if (dto.ScopeId <= 0)
                return BadRequest(new { message = "Portée invalide." });
            if (dto.Sections == ShareLinkSection.Aucune)
                return BadRequest(new { message = "Sélectionnez au moins une section à partager." });
            if (dto.MaxUses is <= 0)
                return BadRequest(new { message = "Le nombre d'ouvertures doit être positif." });

            // `await` obligatoire : sans lui, le pattern teste l'objet Task, qui
            // est toujours non nul — le filtre ne serait jamais réellement validé.
            if (await ValiderFiltres(dto.Filters) is { } erreur)
                return BadRequest(new { message = erreur });

            var resultat = await _service.CreerAsync(dto, userId, HttpContext.RequestAborted);
            if (resultat == null)
                return BadRequest(new { message = "Portée introuvable." });

            return StatusCode(StatusCodes.Status201Created, resultat);
        }

        /// <summary>
        /// Valide les filtres figés du lien avant enregistrement.
        /// </summary>
        /// <remarks>
        /// Les identifiants d'articles sont vérifiés un par un contre la base, et
        /// non par simple recherche d'appartenance : on veut distinguer « article
        /// inconnu ou inactif » d'une simple liste valide. Le refus est explicite,
        /// car un filtre silencieusement ignoré exposerait plus large que prévu.
        /// </remarks>
        private async Task<string?> ValiderFiltres(ShareLinkFiltersDto? filtres)
        {
            if (filtres == null) return null;

            if (filtres.ArticleIds is { Count: > 0 })
            {
                var ids = filtres.ArticleIds;

                if (ids.Any(i => i <= 0))
                    return "Un identifiant d'article est invalide.";

                if (ids.Count > MaxArticlesPartages)
                    return $"Un lien ne peut pas porter plus de {MaxArticlesPartages} articles.";

                var doublons = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (doublons.Count > 0)
                    return "La sélection d'articles contient des doublons.";

                var existants = await _db.Articles
                    .Where(a => ids.Contains(a.Id))
                    .Select(a => a.Id)
                    .ToListAsync(HttpContext.RequestAborted);

                var manquants = ids.Except(existants).ToList();
                if (manquants.Count > 0)
                    return "Article inconnu : " + string.Join(", ", manquants);

                var inactifs = await _db.Articles
                    .Where(a => ids.Contains(a.Id) && !a.EstActif)
                    .Select(a => a.Id)
                    .ToListAsync(HttpContext.RequestAborted);

                if (inactifs.Count > 0)
                    return "Article inactif, à réactiver avant de le partager : " + string.Join(", ", inactifs);
            }

            if (!string.IsNullOrWhiteSpace(filtres.TypeStock)
                && !Enum.TryParse<TypeStock>(filtres.TypeStock.Trim(), ignoreCase: true, out _))
            {
                return "TypeStock doit valoir Libre, Reserve ou Importe.";
            }

            return null;
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

        private static ObjectResult RefusStock() => new(new
        {
            message = "Vous n'avez pas le droit de partager du stock. Le droit « gérer le stock » vous est nécessaire."
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