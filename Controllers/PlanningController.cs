using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Filters;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Grille « Planning interne » — reprodction de PLANNING INTERNE - Copie.xlsx :
    /// colonnes = chaînes de production (sous-traitance), lignes = samedis hebdo,
    /// cellules = commandes de matelas (ex. 79-PO33341). Chaque changement crée
    /// une Notification pour chaque utilisateur (cloche temps réel).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PlanningController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<PlanningHub> _hub;

        public PlanningController(ApplicationDbContext context, IHubContext<PlanningHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        /// <summary>GET : la grille complète — chaînes (colonnes) + cellules (chaîne, samedi, commande).</summary>
        [HttpGet]
        [RequireModulePermission("planning", requireWrite: false)]
        public async Task<ActionResult> GetGrille()
        {
            var chaines = await _context.ChainesProduction
                .Where(c => c.EstActif)
                .OrderBy(c => c.Nom)
                .Select(c => new { c.Id, c.Nom, Type = c.TypeChaine.ToString() })
                .ToListAsync();

            var cellules = await _context.PlanningEntries
                .OrderBy(p => p.DateSamedi)
                .ThenBy(p => p.ChaineProductionId)
                .Select(p => new
                {
                    p.Id,
                    p.ChaineProductionId,
                    p.DateSamedi,
                    p.NumeroCommande,
                    p.Quantite,
                    p.EstLivree,
                    p.Notes
                })
                .ToListAsync();

            return Ok(new { chaines, cellules });
        }

        /// <summary>POST : crée (ou met à jour) une cellule ; notifie tous les utilisateurs.</summary>
        [HttpPost]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> PostCellule([FromBody] PlanningEntryDto dto)
        {
            var entry = new PlanningEntry
            {
                ChaineProductionId = dto.ChaineProductionId,
                DateSamedi = dto.DateSamedi,
                NumeroCommande = dto.NumeroCommande,
                Quantite = dto.Quantite,
                EstLivree = dto.EstLivree,
                Notes = dto.Notes
            };
            _context.PlanningEntries.Add(entry);
            await _context.SaveChangesAsync();
            await NotifierAsync($"Le planning a changé : cellule {(dto.ChaineProductionId.HasValue ? dto.ChaineProductionId.Value.ToString() : "—")} le {dto.DateSamedi:dd/MM/yyyy}, commande {dto.NumeroCommande}", entry.Id);
            return CreatedAtAction(nameof(PostCellule), new { id = entry.Id }, entry);
        }

        /// <summary>PUT : modifie une cellule (ex. marquer livrée) et notifie.</summary>
        [HttpPut("{id}")]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> PutCellule(int id, [FromBody] PlanningEntryDto dto)
        {
            var entry = await _context.PlanningEntries.FindAsync(id);
            if (entry == null) return NotFound();
            entry.ChaineProductionId = dto.ChaineProductionId;
            entry.DateSamedi = dto.DateSamedi;
            entry.NumeroCommande = dto.NumeroCommande;
            entry.Quantite = dto.Quantite;
            entry.EstLivree = dto.EstLivree;
            entry.Notes = dto.Notes;
            await _context.SaveChangesAsync();
            await NotifierAsync($"Le planning a changé : cellule mise à jour le {dto.DateSamedi:dd/MM/yyyy}, commande {dto.NumeroCommande}", entry.Id);
            return NoContent();
        }

        /// <summary>DELETE : supprime une cellule et notifie.</summary>
        [HttpDelete("{id}")]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> DeleteCellule(int id)
        {
            var entry = await _context.PlanningEntries.FindAsync(id);
            if (entry == null) return NotFound();
            _context.PlanningEntries.Remove(entry);
            await _context.SaveChangesAsync();
            await NotifierAsync($"Le planning a changé : cellule supprimée (id {id})", null);
            return NoContent();
        }

        /// <summary>Crée une Notification pour CHAQUE utilisateur + push SignalR temps réel.</summary>
        private async Task NotifierAsync(string message, int? planningEntryId)
        {
            var users = await _context.Users.ToListAsync();
            var notifs = users.Select(u => new Notification
            {
                UtilisateurId = u.Id,
                Message = message,
                DateNotification = DateTime.Now,
                EstLivree = false,
                PlanningEntryId = planningEntryId
            }).ToList();
            _context.Notifications.AddRange(notifs);
            await _context.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("PlanningChanged", message);
        }
    }

    public class PlanningEntryDto
    {
        public int? ChaineProductionId { get; set; }
        public DateTime DateSamedi { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public int? Quantite { get; set; }
        public bool EstLivree { get; set; }
        public string? Notes { get; set; }
    }
}
