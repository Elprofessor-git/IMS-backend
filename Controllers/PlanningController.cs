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
    /// Grille « Planning interne » — reproduction de PLANNING INTERNE - Copie.xlsx :
    /// colonnes = chaînes de production (sous-traitance), lignes = dates d'export (libres),
    /// cellules = commandes de matelas (ex. 79-PO33341). Chaque changement crée une
    /// Notification pour chaque utilisateur (cloche temps réel).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PlanningController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<PlanningHub> _hub;
        private readonly ILogger<PlanningController> _logger;

        public PlanningController(ApplicationDbContext context, IHubContext<PlanningHub> hub, ILogger<PlanningController> logger)
        {
            _context = context;
            _hub = hub;
            _logger = logger;
        }

        /// <summary>
        /// GET : la grille complète — chaînes (colonnes) + dates (lignes) + cellules (chaîne, date, commande).
        /// Les dates sont celles de la table PlanningDates ; une date présente dans une cellule mais absente
        /// de la table est ajoutée défensivement (id 0 = pas encore une vraie ligne), pour ne jamais masquer
        /// une cellule existante.
        /// </summary>
        [HttpGet]
        [RequireModulePermission("planning", requireWrite: false)]
        public async Task<ActionResult> GetGrille()
        {
            var chaines = await _context.ChainesProduction
                .Where(c => c.EstActif)
                .OrderBy(c => c.Nom)
                .Select(c => new { c.Id, c.Nom, Type = c.TypeChaine.ToString() })
                .ToListAsync();

            var lignesDates = await _context.PlanningDates
                .OrderBy(d => d.Date)
                .Select(d => new { d.Id, d.Date })
                .ToListAsync();

            // Une cellule n'est affichable que si elle est rattachée à une chaîne
            // visible de la grille. Les cellules orphelines (sans chaîne) ne sont
            // pas des commandes de la grille : leur date ne doit jamais produire
            // de ligne, et elles ne doivent pas gonfler les totaux.
            var chaineIds = chaines.Select(c => c.Id).ToHashSet();
            var cellules = await _context.PlanningEntries
                .Where(p => p.ChaineProductionId != null && chaineIds.Contains(p.ChaineProductionId.Value))
                .OrderBy(p => p.DateSamedi)
                .ThenBy(p => p.ChaineProductionId)
                .ThenBy(p => p.Id)
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

            // Une ligne dérivée (id = 0) ne doit exister que pour porter une cellule
            // réelle affichable. La « pseudo-ligne défensive » du lot 2 est donc
            // bornée : une date qui n'est ni une vraie ligne, ni portée par une
            // cellule visible, disparaît de la réponse. DateTime.MinValue n'est
            // jamais une vraie semaine d'export (valeur d'horloge non initialisée).
            var datesDeCellulesVues = cellules
                .Select(p => NormaliserDate(p.DateSamedi))
                .ToHashSet();

            var lignesParDate = lignesDates.ToDictionary(l => NormaliserDate(l.Date));
            var toutesDates = lignesDates
                .Select(l => NormaliserDate(l.Date))
                .Union(datesDeCellulesVues)
                .Where(d => d != DateTime.MinValue)
                .Where(d => lignesParDate.ContainsKey(d) || datesDeCellulesVues.Contains(d))
                .Distinct()
                .OrderBy(d => d)
                .Select(d => new
                {
                    Id = lignesParDate.TryGetValue(d, out var ligne) ? ligne.Id : 0,
                    Date = d
                })
                .ToList();

            return Ok(new { chaines, dates = toutesDates, cellules });
        }

        /// <summary>POST : ajoute une ligne de date au planning. La date doit être libre (unicité).</summary>
        [HttpPost("dates")]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> PostDate([FromBody] PlanningDateDto dto)
        {
            if (dto.Date == default)
                return BadRequest("La date est obligatoire.");

            var date = NormaliserDate(dto.Date);

            if (await _context.PlanningDates.AnyAsync(d => d.Date == date))
                return Conflict($"La date {date:dd/MM/yyyy} existe déjà dans le planning.");

            var ligne = new PlanningDate { Date = date };
            _context.PlanningDates.Add(ligne);
            await _context.SaveChangesAsync();
            await NotifierAsync($"Le planning a changé : ajout de la date {date:dd/MM/yyyy}", null);
            return CreatedAtAction(nameof(GetGrille), new { }, new { ligne.Id, ligne.Date });
        }

        /// <summary>
        /// PUT : déplace une ligne de date (les cellules qu'elle contient suivent).
        /// Refus 409 si la date cible est déjà une autre ligne.
        /// </summary>
        [HttpPut("dates/{id}")]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> PutDate(int id, [FromBody] PlanningDateDto dto)
        {
            var ligne = await _context.PlanningDates.FindAsync(id);
            if (ligne == null) return NotFound();

            if (dto.Date == default)
                return BadRequest("La date est obligatoire.");

            var date = NormaliserDate(dto.Date);

            if (await _context.PlanningDates.AnyAsync(d => d.Id != id && d.Date == date))
                return Conflict($"La date {date:dd/MM/yyyy} existe déjà dans le planning.");

            var cellules = await _context.PlanningEntries
                .Where(p => p.DateSamedi == ligne.Date)
                .ToListAsync();
            foreach (var cellule in cellules)
                cellule.DateSamedi = date;

            ligne.Date = date;
            await _context.SaveChangesAsync();
            await NotifierAsync($"Le planning a changé : la date {cellules.Count} cellule(s) concernée(s) a/ont été déplacées vers {date:dd/MM/yyyy}", null);
            return NoContent();
        }

        /// <summary>DELETE : supprime une ligne de date ainsi que les cellules qu'elle contenait.</summary>
        [HttpDelete("dates/{id}")]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> DeleteDate(int id)
        {
            var ligne = await _context.PlanningDates.FindAsync(id);
            if (ligne == null) return NotFound();

            var cellules = await _context.PlanningEntries
                .Where(p => p.DateSamedi == ligne.Date)
                .ToListAsync();

            _context.PlanningEntries.RemoveRange(cellules);
            _context.PlanningDates.Remove(ligne);
            await _context.SaveChangesAsync();
            await NotifierAsync($"Le planning a changé : suppression de la date {ligne.Date:dd/MM/yyyy} (et de ses {cellules.Count} cellule(s))", null);
            return NoContent();
        }

        private static DateTime NormaliserDate(DateTime d)
        {
            return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>POST : crée (ou met à jour) une cellule ; notifie tous les utilisateurs.</summary>
        [HttpPost]
        [RequireModulePermission("planning", requireWrite: true)]
        public async Task<ActionResult> PostCellule([FromBody] PlanningEntryDto dto)
        {
            if (dto.DateSamedi == default)
                return BadRequest("La date de la cellule est obligatoire.");

            if (dto.ChaineProductionId == null)
                return BadRequest("La cellule doit être rattachée à une chaîne.");

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

            if (dto.DateSamedi == default)
                return BadRequest("La date de la cellule est obligatoire.");

            if (dto.ChaineProductionId == null)
                return BadRequest("La cellule doit être rattachée à une chaîne.");

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

        /// <summary>Notification AUTOMATIQUE à TOUS les utilisateurs ACTIFS + push SignalR temps réel.</summary>
        /// <remarks>
        /// Règle clé : l'écriture planning ne doit JAMAIS échouer à cause de la notification.
        /// Best effort volontaire (même règle que PrixHistoriqueService) : toute erreur ici
        /// est loggée puis ignorée. Une seule SaveChanges pour toutes les notifications.
        /// </remarks>
        private async Task NotifierAsync(string message, int? planningEntryId)
        {
            try
            {
                var utilisateursActifs = await _context.Users
                    .Where(u => u.EstActif)
                    .ToListAsync();

                if (utilisateursActifs.Count == 0)
                    return;

                var notifs = utilisateursActifs.Select(u => new Notification
                {
                    UtilisateurId = u.Id,
                    Message = message,
                    DateNotification = DateTime.Now,
                    EstLivree = false,
                    PlanningEntryId = planningEntryId
                }).ToList();

                _context.Notifications.AddRange(notifs);
                await _context.SaveChangesAsync();

                await _hub.Clients.Group("PlanningClients").SendAsync("PlanningChanged", message, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec (ignoré) de la notification planning : {Message}", message);
            }
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

    public class PlanningDateDto
    {
        public DateTime Date { get; set; }
    }
}
