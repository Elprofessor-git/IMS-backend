using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
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

        [HttpGet]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<TacheProduction>>> GetTaches()
        {
            return await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .ThenInclude(c => c.Client)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireModulePermission("taches", requireWrite: false)]
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

        [HttpGet("Statut/{statut}")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<TacheProduction>>> GetTachesByStatut(StatutTache statut)
        {
            return await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .ThenInclude(c => c.Client)
                .Where(t => t.Statut == statut)
                .ToListAsync();
        }

        [HttpGet("Equipe/{equipe}")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<TacheProduction>>> GetTachesByEquipe(string equipe)
        {
            return await _context.TachesProduction
                .Include(t => t.CommandeClient)
                .ThenInclude(c => c.Client)
                .Where(t => t.EquipeAssignee == equipe)
                .ToListAsync();
        }

        [HttpGet("Dashboard")]
        [RequireModulePermission("taches", requireWrite: false)]
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

        [HttpPost]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult<TacheProduction>> PostTacheProduction(TacheProduction tache)
        {
            tache.DateCreation = DateTime.Now;
            _context.TachesProduction.Add(tache);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTacheProduction", new { id = tache.Id }, tache);
        }

        [HttpPut("{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        [HttpPut("{id}/statut")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        [HttpPut("{id}/equipe")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> AssignerEquipe(int id, [FromBody] AssignerEquipeDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            tache.EquipeAssignee = data.EquipeId;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/Commencer")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        [HttpPost("{id}/MettreAJourAvancement")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        [HttpPost("{id}/Bloquer")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        [HttpPost("{id}/Debloquer")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        [HttpPost("{id}/Terminer")]
        [RequireModulePermission("taches", requireWrite: true)]
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

            // Découplage LOT 8 : la clôture automatique de la commande ne relève
            // plus de TacheProduction (module Magasin). Elle dépend désormais du
            // solde comptable qualité : Q = ΣA + ΣB sur TOUS les triplets
            // (OF, Chaîne, Taille) de la commande — effectué après chaque
            // ControleQualite par QualiteService.CloturerCommandeSiSoldee.
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche terminée avec succès" });
        }

        [HttpPost("{id}/Assigner")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> AssignerTache(int id, [FromBody] AssignerTacheDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            tache.ResponsableAssigne = data.AssigneA;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Tâche assignée avec succès" });
        }

        [HttpPost("{id}/ModifierPriorite")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        [HttpPost("{id}/ModifierEcheance")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> ModifierEcheance(int id, [FromBody] ModifierEcheanceDto data)
        {
            var tache = await _context.TachesProduction.FindAsync(id);
            if (tache == null) return NotFound();

            tache.DateFinPrevue = data.DateFinPrevue;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Échéance mise à jour" });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
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

        // ───────────────────────────── Groupes de tâches ─────────────────────────────
        // Groupe = modèle répétitif de tâches, applicable à une commande : l'application
        // génère une TacheProduction par ligne (GroupeTacheId posé pour la traçabilité).

        [HttpGet("Groupes")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<GroupeTacheDto>>> GetGroupes()
        {
            var groupes = await _context.GroupesTaches
                .Include(g => g.Lignes)
                .Include(g => g.Taches)
                .OrderByDescending(g => g.DateCreation)
                .ToListAsync();

            return Ok(groupes.Select(g => new GroupeTacheDto
            {
                Id = g.Id,
                Nom = g.Nom,
                Description = g.Description,
                EstActif = g.EstActif,
                DateCreation = g.DateCreation,
                Lignes = g.Lignes.OrderBy(l => l.Ordre).Select(l => new GroupeTacheLigneDto
                {
                    Id = l.Id,
                    GroupeTacheId = l.GroupeTacheId,
                    Titre = l.Titre,
                    Description = l.Description,
                    EquipeAssignee = l.EquipeAssignee,
                    ResponsableAssigne = l.ResponsableAssigne,
                    Priorite = (int)l.Priorite,
                    Ordre = l.Ordre,
                    DureeEstimeeHeures = l.DureeEstimeeHeures,
                }).ToList(),
                NombreCommandesAppliquees = g.Taches.Where(t => t.CommandeClientId.HasValue).Select(t => t.CommandeClientId).Distinct().Count(),
                NombreTachesGenerees = g.Taches.Count,
            }).ToList());
        }

        [HttpPost("Groupes")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult<GroupeTache>> CreateGroupe([FromBody] CreateGroupeTacheDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nom))
                return BadRequest(new { message = "Le nom du groupe est requis." });

            var groupe = new GroupeTache { Nom = dto.Nom.Trim(), Description = dto.Description };
            _context.GroupesTaches.Add(groupe);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Groupe créé", id = groupe.Id });
        }

        [HttpPut("Groupes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> UpdateGroupe(int id, [FromBody] UpdateGroupeTacheDto dto)
        {
            var groupe = await _context.GroupesTaches.FindAsync(id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });

            if (dto.Nom != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Nom))
                    return BadRequest(new { message = "Le nom du groupe est requis." });
                groupe.Nom = dto.Nom.Trim();
            }
            if (dto.Description != null) groupe.Description = dto.Description;
            if (dto.EstActif.HasValue) groupe.EstActif = dto.EstActif.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Groupe mis à jour" });
        }

        [HttpDelete("Groupes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> DeleteGroupe(int id)
        {
            var groupe = await _context.GroupesTaches
                .Include(g => g.Lignes)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });

            // Les tâches générées survivent (GroupeTacheId -> SetNull).
            _context.GroupesTaches.Remove(groupe);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Groupe supprimé (tâches générées conservées)" });
        }

        [HttpPost("Groupes/{id}/Lignes")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult> AddLigneGroupe(int id, [FromBody] CreateGroupeTacheLigneDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Titre))
                return BadRequest(new { message = "Le titre de la ligne est requis." });

            var groupe = await _context.GroupesTaches.FindAsync(id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });

            var ordre = dto.Ordre;
            if (ordre <= 0)
                ordre = (await _context.GroupesTachesLignes.MaxAsync(l => (int?)l.Ordre) ?? 0) + 1;

            var ligne = new GroupeTacheLigne
            {
                GroupeTacheId = id,
                Titre = dto.Titre.Trim(),
                Description = dto.Description,
                EquipeAssignee = dto.EquipeAssignee,
                ResponsableAssigne = dto.ResponsableAssigne,
                Priorite = (PrioriteTache)Math.Clamp(dto.Priorite, 0, 3),
                Ordre = ordre,
                DureeEstimeeHeures = dto.DureeEstimeeHeures,
            };
            _context.GroupesTachesLignes.Add(ligne);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne ajoutée au groupe", id = ligne.Id });
        }

        [HttpPut("Lignes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult> UpdateLigneGroupe(int id, [FromBody] UpdateGroupeTacheLigneDto dto)
        {
            var ligne = await _context.GroupesTachesLignes.FindAsync(id);
            if (ligne == null)
                return NotFound(new { message = "Ligne introuvable." });

            if (dto.Titre != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Titre))
                    return BadRequest(new { message = "Le titre de la ligne est requis." });
                ligne.Titre = dto.Titre.Trim();
            }
            if (dto.Description != null) ligne.Description = dto.Description;
            if (dto.EquipeAssignee != null) ligne.EquipeAssignee = dto.EquipeAssignee;
            if (dto.ResponsableAssigne != null) ligne.ResponsableAssigne = dto.ResponsableAssigne;
            if (dto.Priorite.HasValue) ligne.Priorite = (PrioriteTache)Math.Clamp(dto.Priorite.Value, 0, 3);
            if (dto.Ordre.HasValue) ligne.Ordre = dto.Ordre.Value;
            if (dto.DureeEstimeeHeures.HasValue) ligne.DureeEstimeeHeures = dto.DureeEstimeeHeures.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne mise à jour" });
        }

        [HttpDelete("Lignes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult> DeleteLigneGroupe(int id)
        {
            var ligne = await _context.GroupesTachesLignes.FindAsync(id);
            if (ligne == null)
                return NotFound(new { message = "Ligne introuvable." });

            _context.GroupesTachesLignes.Remove(ligne);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne supprimée du groupe" });
        }

        /// <summary>
        /// Applique un groupe à une commande : génère une TacheProduction par ligne
        /// (statut NonCommence, priorité de la ligne, commande renseignée, GroupeTacheId posé).
        /// Retourne les tâches créées.
        /// </summary>
        [HttpPost("Groupes/{id}/Appliquer")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult> AppliquerGroupe(int id, [FromBody] AppliquerGroupeTacheDto dto)
        {
            var groupe = await _context.GroupesTaches
                .Include(g => g.Lignes)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });
            if (!groupe.EstActif)
                return BadRequest(new { message = "Le groupe est inactif : impossible de l'appliquer." });

            var commande = await _context.CommandesClients.FindAsync(dto.CommandeId);
            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });
            if (groupe.Lignes.Count == 0)
                return BadRequest(new { message = "Le groupe ne contient aucune ligne à appliquer." });

            var generees = new List<TacheProduction>();
            foreach (var ligne in groupe.Lignes.OrderBy(l => l.Ordre))
            {
                var tache = new TacheProduction
                {
                    Titre = ligne.Titre,
                    Description = ligne.Description,
                    CommandeClientId = dto.CommandeId,
                    EquipeAssignee = ligne.EquipeAssignee,
                    ResponsableAssigne = ligne.ResponsableAssigne,
                    Statut = StatutTache.NonCommence,
                    Priorite = ligne.Priorite,
                    DureeEstimeeHeures = ligne.DureeEstimeeHeures,
                    GroupeTacheId = groupe.Id,
                };
                _context.TachesProduction.Add(tache);
                generees.Add(tache);
            }
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"{generees.Count} tâche(s) générée(s) pour la commande {commande.NumeroCommande}",
                count = generees.Count,
                ids = generees.Select(t => t.Id).ToList(),
            });
        }
    }
}
