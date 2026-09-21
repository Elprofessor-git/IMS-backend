using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Filters;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Cloche de notifications de l'utilisateur courant. L'utilisateur voit ses
    /// notifications non livrées et peut les marquer « livrées » (lues) une à une.
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
        [RequireModulePermission("planning", requireWrite: false)]
        public async Task<ActionResult> GetMesNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
            if (userId == null) return Unauthorized();

            var toutes = await _context.Notifications
                .Where(n => n.UtilisateurId.ToString() == userId)
                .OrderByDescending(n => n.DateNotification)
                .ToListAsync();

            var nonLivrees = toutes.Where(n => !n.EstLivree).ToList();
            return Ok(new
            {
                notifications = toutes,
                countNonLivrees = nonLivrees.Count
            });
        }

        /// <summary>POST api/notification/{id}/livrer : marque la notification comme livrée.</summary>
        [HttpPost("{id}/livrer")]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> Livrer(int id)
        {
            var n = await _context.Notifications.FindAsync(id);
            if (n == null) return NotFound();
            n.EstLivree = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
