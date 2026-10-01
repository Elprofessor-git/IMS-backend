using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Cloche de notifications de l'utilisateur courant. L'utilisateur voit SES
    /// notifications et peut les marquer « livrées » (lues) une à une ou toutes.
    /// Accessible à tout utilisateur authentifié (sans gate planning) : la cloche
    /// est un mécanisme transversal, indépendant de la permission du module source.
    ///
    /// Règle d'isolation (inchangée, mais désormais vérifiable route par route) : le
    /// destinataire n'est JAMAIS reçu du client — il vient du claim NameIdentifier, et
    /// toutes les lectures/écritures filtrent sur ce même identifiant. Une notification
    /// d'autrui est donc inexistante pour l'appelant : 404, jamais 403, afin de ne pas
    /// divulguer son existence.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;

        /// <summary>
        /// GET api/notification/me : notifications de l'utilisateur + compteur des non
        /// livrées. Paginé (page/pageSize, défauts 1/50, plafond 200) pour ne jamais
        /// charger tout l'historique en mémoire. Rétro-compatible : les champs
        /// `notifications` et `countNonLivrees` gardent exactement le même sens ; les
        /// champs de pagination sont ADDITIFS. `countNonLivrees` reste le total (toutes
        /// pages confondues), pour que le badge de la cloche soit exact.
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult> GetMesNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var userId = CurrentUserId;
            if (userId == null) return Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            // Filtrage SQL sur le demandeur, puis mapping en mémoire : c'est ce mapping
            // qui transforme l'énumération Type en TEXTE pour le frontend.
            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UtilisateurId == userId);

            var totalCount = await query.CountAsync();
            var countNonLivrees = await query.CountAsync(n => !n.EstLivree);

            var lignes = await query
                .OrderByDescending(n => n.DateNotification)
                .ThenByDescending(n => n.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pageData = lignes.Select(Map).ToList();

            return Ok(new
            {
                notifications = pageData,
                countNonLivrees,
                pageNumber = page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// GET api/notification/{id} : lecture d'UNE notification, parmi les siennes.
        /// Le filtre sur UtilisateurId est appliqué DANS la requête : un identifiant
        /// deviné (IDOR) renvoie 404, comme une notification inexistante.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult> GetNotification(int id)
        {
            var userId = CurrentUserId;
            if (userId == null) return Unauthorized();

            var ligne = await _context.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.UtilisateurId == userId);

            if (ligne == null) return NotFound();
            return Ok(Map(ligne));
        }

        private static NotificationDto Map(Notification source) => new()
        {
            Id = source.Id,
            Message = source.Message,
            DateNotification = source.DateNotification,
            EstLivree = source.EstLivree,
            Type = source.Type.ToString(),
            PlanningEntryId = source.PlanningEntryId,
            TacheProductionId = source.TacheProductionId,
            GmailMessageId = source.GmailMessageId
        };

        /// <summary>POST api/notification/{id}/livrer : marque SA PROPRE notification comme livrée.</summary>
        [HttpPost("{id:int}/livrer")]
        public async Task<ActionResult> Livrer(int id)
        {
            var userId = CurrentUserId;
            if (userId == null) return Unauthorized();

            var n = await _context.Notifications
                .FirstOrDefaultAsync(x => x.Id == id && x.UtilisateurId == userId);
            if (n == null) return NotFound();

            n.EstLivree = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>POST api/notification/livrer-toutes : marque toutes ses notifications non livrées comme livrées.</summary>
        [HttpPost("livrer-toutes")]
        public async Task<ActionResult> LivrerToutes()
        {
            var userId = CurrentUserId;
            if (userId == null) return Unauthorized();

            var nonLivrees = await _context.Notifications
                .Where(n => n.UtilisateurId == userId && !n.EstLivree)
                .ToListAsync();
            if (nonLivrees.Count == 0) return NoContent();

            foreach (var n in nonLivrees)
                n.EstLivree = true;

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    /// <summary>
    /// Projection de la cloche. <see cref="Type"/> est volontairement une CHAÎNE
    /// (« Planning », « TacheAssignee », « TacheDesassignee », « TacheDepuisEmail ») :
    /// la cloche s'appuie dessus pour l'icône et la route, et non sur l'ordre d'une
    /// énumération. Les identifiants sont des cibles de navigation, jamais des URL : le
    /// frontend ne construit que des routes internes à l'application.
    /// </summary>
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime DateNotification { get; set; }
        public bool EstLivree { get; set; }
        public string Type { get; set; } = nameof(TypeNotification.Planning);
        public int? PlanningEntryId { get; set; }
        public int? TacheProductionId { get; set; }
        public int? GmailMessageId { get; set; }
    }
}
