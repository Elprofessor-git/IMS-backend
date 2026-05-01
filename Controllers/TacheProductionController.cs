using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Dtos;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TacheProductionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TacheProductionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/TacheProduction
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TacheProduction>>> GetTaches()
        {
            return await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .ThenInclude(c => c.Client)
                .ToListAsync();
        }

        // GET: api/TacheProduction/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TacheProduction>> GetTacheProduction(int id)
        {
            var tache = await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .ThenInclude(c => c.Client)
                .Include(t => t.MouvementsStock)
                .ThenInclude(m => m.Stock)
                .ThenInclude(s => s.Article)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tache == null)
            {
                return NotFound();
            }

            return tache;
        }

        // GET: api/TacheProduction/Statut/EnCours
        [HttpGet("Statut/{statut}")]
        public async Task<ActionResult<IEnumerable<TacheProduction>>> GetTachesByStatut(StatutTache statut)
        {
            return await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .ThenInclude(c => c.Client)
                .Where(t => t.Statut == statut)
                .ToListAsync();
        }

        // GET: api/TacheProduction/Equipe/Production1
        [HttpGet("Equipe/{equipe}")]
        public async Task<ActionResult<IEnumerable<TacheProduction>>> GetTachesByEquipe(string equipe)
        {
            return await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .ThenInclude(c => c.Client)
                .Where(t => t.EquipeAssignee == equipe)
                .ToListAsync();
        }

        // GET: api/TacheProduction/Dashboard
        [HttpGet("Dashboard")]
        public async Task<ActionResult<object>> GetDashboard()
        {
            var dashboard = new
            {
                TotalTaches = await _context.TachesProduction.CountAsync(),
                NonCommencees = await _context.TachesProduction.CountAsync(t => t.Statut == StatutTache.NonCommence),
                EnCours = await _context.TachesProduction.CountAsync(t => t.Statut == StatutTache.EnCours),
                Bloquees = await _context.TachesProduction.CountAsync(t => t.Statut == StatutTache.Bloque),
                Terminees = await _context.TachesProduction.CountAsync(t => t.Statut == StatutTache.Termine),
                TachesUrgentes = await _context.TachesProduction.CountAsync(t => t.Priorite == PrioriteTache.Urgente && t.Statut != StatutTache.Termine),
                TachesEnRetard = await _context.TachesProduction.CountAsync(t => t.DateFinPrevue < DateTime.Now && t.Statut != StatutTache.Termine),
                AvancementMoyen = await _context.TachesProduction
                    .Where(t => t.Statut != StatutTache.Termine && t.Statut != StatutTache.Annule)
                    .AverageAsync(t => (double?)t.PourcentageAvancement) ?? 0
            };

            return Ok(dashboard);
        }

        // POST: api/TacheProduction
        [HttpPost]
        public async Task<ActionResult<TacheProduction>> PostTacheProduction(TacheProduction tache)
        {
            tache.DateCreation = DateTime.Now;
            _context.TachesProduction.Add(tache);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTacheProduction", new { id = tache.Id }, tache);
        }

        // PUT: api/TacheProduction/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTacheProduction(int id, TacheProduction tache)
        {
            if (id != tache.Id)
            {
                return BadRequest();
            }

            tache.DateMiseAJour = DateTime.Now;
            _context.Entry(tache).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TacheProductionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/TacheProduction/5/Commencer
        [HttpPost("{id}/Commencer")]
        public async Task<IActionResult> CommencerTache(int id, [FromBody] string responsable)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null)
            {
                return NotFound();
            }

            tache.Statut = StatutTache.EnCours;
            tache.DateDebutReelle = DateTime.Now;
            tache.ResponsableAssigne = responsable;
            tache.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche commencée avec succès" });
        }

        // POST: api/TacheProduction/5/MettreAJourAvancement
        [HttpPost("{id}/MettreAJourAvancement")]
        public async Task<IActionResult> MettreAJourAvancement(int id, [FromBody] decimal pourcentage)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null)
            {
                return NotFound();
            }

            tache.PourcentageAvancement = Math.Max(0, Math.Min(100, pourcentage));
            tache.DateMiseAJour = DateTime.Now;

            if (tache.PourcentageAvancement >= 100)
            {
                tache.Statut = StatutTache.Termine;
                tache.DateFinReelle = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Avancement mis à jour avec succès" });
        }

        // POST: api/TacheProduction/5/Bloquer
        [HttpPost("{id}/Bloquer")]
        public async Task<IActionResult> BloquerTache(int id, [FromBody] string motif)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null)
            {
                return NotFound();
            }

            tache.Statut = StatutTache.Bloque;
            tache.ProblemesBloques = motif;
            tache.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche bloquée avec succès" });
        }

        // POST: api/TacheProduction/5/Debloquer
        [HttpPost("{id}/Debloquer")]
        public async Task<IActionResult> DebloquerTache(int id)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null)
            {
                return NotFound();
            }

            tache.Statut = StatutTache.EnCours;
            tache.ProblemesBloques = null;
            tache.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche débloquée avec succès" });
        }

        // POST: api/TacheProduction/5/Terminer
        [HttpPost("{id}/Terminer")]
        public async Task<IActionResult> TerminerTache(int id, [FromBody] string notes)
        {
            var tache = await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tache == null)
            {
                return NotFound();
            }

            tache.Statut = StatutTache.Termine;
            tache.DateFinReelle = DateTime.Now;
            tache.PourcentageAvancement = 100;
            tache.NotesProgression = notes;
            tache.DateMiseAJour = DateTime.Now;

            // Mettre à jour le statut de la commande si toutes les tâches sont terminées
            if (tache.CommandeClient != null)
            {
                var tachesRestantes = await _context.TachesProduction
                    .CountAsync(t => t.CommandeClientId == tache.CommandeClientId && 
                                    t.Statut != StatutTache.Termine && 
                                    t.Statut != StatutTache.Annule);

                if (tachesRestantes == 0)
                {
                    tache.CommandeClient.Statut = StatutCommande.Terminee;
                    tache.CommandeClient.DateMiseAJour = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche terminée avec succès" });
        }

        // PUT: api/TacheProduction/5/statut
        [HttpPut("{id}/statut")]
        public async Task<IActionResult> UpdateStatut(int id, [FromBody] UpdateStatutDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            if (Enum.TryParse<StatutTache>(data.Statut, out var statut))
            {
                tache.Statut = statut;
                tache.DateMiseAJour = DateTime.Now;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            return BadRequest("Statut invalide");
        }

        // PUT: api/TacheProduction/5/equipe
        [HttpPut("{id}/equipe")]
        public async Task<IActionResult> AssignerEquipe(int id, [FromBody] AssignerEquipeDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            tache.EquipeAssignee = data.EquipeId;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/TacheProduction/5/Assigner
        [HttpPost("{id}/Assigner")]
        public async Task<IActionResult> AssignerTache(int id, [FromBody] AssignerTacheDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            tache.ResponsableAssigne = data.AssigneA;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Tâche assignée avec succès" });
        }

        // POST: api/TacheProduction/5/ModifierPriorite
        [HttpPost("{id}/ModifierPriorite")]
        public async Task<IActionResult> ModifierPriorite(int id, [FromBody] ModifierPrioriteDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            if (Enum.TryParse<PrioriteTache>(data.Priorite, out var priorite))
            {
                tache.Priorite = priorite;
                tache.DateMiseAJour = DateTime.Now;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Priorité mise à jour" });
            }
            return BadRequest("Priorité invalide");
        }

        // POST: api/TacheProduction/5/ModifierEcheance
        [HttpPost("{id}/ModifierEcheance")]
        public async Task<IActionResult> ModifierEcheance(int id, [FromBody] ModifierEcheanceDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            tache.DateFinPrevue = data.DateFinPrevue;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Échéance mise à jour" });
        }

        // DELETE: api/TacheProduction/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTacheProduction(int id)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null)
            {
                return NotFound();
            }

            _context.TachesProduction.Remove(tache);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TacheProductionExists(int id)
        {
            return _context.TachesProduction.Any(e => e.Id == id);
        }
    }
}

