using Backend_Gestion_Magasin_API.Data;
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

        /// <summary>GET api/notification/me : notifications de l'utilisateur (non livrées d'abord) + compteur.</summary>
        [HttpGet("me")]
        public async Task<ActionResult> GetMesNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
            if (userId == null) return Unauthorized();

            var toutes = await _context.Notifications
                .Where(n => n.UtilisateurId == userId)
                .OrderByDescending(n => n.DateNotification)
                .Select(n => new
                {
                    n.Id,
                    n.Message,
                    n.DateNotification,
                    n.EstLivree,
                    n.PlanningEntryId
                })
                .ToListAsync();

            var nonLivrees = toutes.Where(n => !n.EstLivree).ToList();
            return Ok(new
            {
                notifications = toutes,
                countNonLivrees = nonLivrees.Count
            });
        }

        /// <summary>POST api/notification/{id}/livrer : marque SA PROPRE notification comme livrée.</summary>
        [HttpPost("{id}/livrer")]
        public async Task<ActionResult> Livrer(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
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
}