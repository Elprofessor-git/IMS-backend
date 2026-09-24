using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Production;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Gamme opératoire d'un OF — module Production (5.3).
    /// Une étape (Coupe/Production/ControleQualite/Expedition) est le jalon suivi
    /// par statut (vocabulaire StatutTache), chaîne, dates prévues/réelles
    /// et temps théorique/réel. Relation à SENS UNIQUE vers PlanningEntry
    /// (planningEntryId optionnel) — aucun second modèle concurrent du planning.
    /// Permissions : module « production ».
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdreFabricationEtapeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OrdreFabricationEtapeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ═══════════ Listage par OF / commande / filtre ═══════════

        [HttpGet]
        [RequireModulePermission("production", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<OrdreFabricationEtapeDto>>> List(
            [FromQuery] int? ofId,
            [FromQuery] int? commandeId,
            [FromQuery] string? typeEtape,
            [FromQuery] string? statut)
        {
            var query = _context.OrdresFabricationEtapes.AsQueryable();
            if (ofId.HasValue)
                query = query.Where(e => e.OrdreFabricationId == ofId.Value);
            if (commandeId.HasValue)
                query = query.Where(e => e.OrdreFabrication.CommandeId == commandeId.Value);
            if (!string.IsNullOrWhiteSpace(typeEtape) && Enum.TryParse<TypeEtapeProduction>(typeEtape, true, out var te))
                query = query.Where(e => e.TypeEtape == te);
            if (!string.IsNullOrWhiteSpace(statut) && Enum.TryParse<StatutTache>(statut, true, out var st))
                query = query.Where(e => e.Statut == st);

            var result = await query
                .OrderBy(e => e.OrdreFabricationId)
                .ThenBy(e => e.DateCreation)
                .Select(e => new OrdreFabricationEtapeDto
                {
                    Id = e.Id,
                    OrdreFabricationId = e.OrdreFabricationId,
                    NumeroOF = e.OrdreFabrication.NumeroOF,
                    CommandeId = e.OrdreFabrication.CommandeId,
                    TypeEtape = e.TypeEtape.ToString(),
                    Statut = e.Statut.ToString(),
                    ChaineProductionId = e.ChaineProductionId,
                    ChaineProductionNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    PlanningEntryId = e.PlanningEntryId,
                    DateDebutPrevue = e.DateDebutPrevue,
                    DateFinPrevue = e.DateFinPrevue,
                    DateDebutReelle = e.DateDebutReelle,
                    DateFinReelle = e.DateFinReelle,
                    TempsTheoriqueHeures = e.TempsTheoriqueHeures,
                    TempsReelHeures = e.TempsReelHeures,
                    ResponsableAssigne = e.ResponsableAssigne,
                    Notes = e.Notes,
                    DateCreation = e.DateCreation,
                })
                .ToListAsync();
            return Ok(result);
        }

        // ═══════════ Détail ═══════════

        [HttpGet("{id}")]
        [RequireModulePermission("production", requireWrite: false)]
        public async Task<ActionResult<OrdreFabricationEtapeDto>> Get(int id)
        {
            var result = await _context.OrdresFabricationEtapes
                .Where(e => e.Id == id)
                .Select(e => new OrdreFabricationEtapeDto
                {
                    Id = e.Id,
                    OrdreFabricationId = e.OrdreFabricationId,
                    NumeroOF = e.OrdreFabrication.NumeroOF,
                    CommandeId = e.OrdreFabrication.CommandeId,
                    TypeEtape = e.TypeEtape.ToString(),
                    Statut = e.Statut.ToString(),
                    ChaineProductionId = e.ChaineProductionId,
                    ChaineProductionNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    PlanningEntryId = e.PlanningEntryId,
                    DateDebutPrevue = e.DateDebutPrevue,
                    DateFinPrevue = e.DateFinPrevue,
                    DateDebutReelle = e.DateDebutReelle,
                    DateFinReelle = e.DateFinReelle,
                    TempsTheoriqueHeures = e.TempsTheoriqueHeures,
                    TempsReelHeures = e.TempsReelHeures,
                    ResponsableAssigne = e.ResponsableAssigne,
                    Notes = e.Notes,
                    DateCreation = e.DateCreation,
                })
                .FirstOrDefaultAsync();
            if (result == null)
                return NotFound(new { message = "Étape d'OF introuvable." });
            return Ok(result);
        }

        // ═══════════ Création ═══════════

        [HttpPost]
        [RequireModulePermission("production", requireWrite: true)]
        public async Task<ActionResult> Create([FromBody] CreateOrdreFabricationEtapeDto dto)
        {
            if (!Enum.TryParse<TypeEtapeProduction>(dto.TypeEtape, true, out var typeEtape))
                return BadRequest(new { message = $"Type d'étape invalide : '{dto.TypeEtape}'." });

            if (!Enum.TryParse<StatutTache>(dto.Statut, true, out var statut))
                return BadRequest(new { message = $"Statut invalide : '{dto.Statut}'." });

            if (!await _context.OrdresFabrication.AnyAsync(of => of.Id == dto.OrdreFabricationId))
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            if (dto.ChaineProductionId.HasValue
                && !await _context.ChainesProduction.AnyAsync(cp => cp.Id == dto.ChaineProductionId.Value))
                return BadRequest(new { message = "Chaîne de production introuvable." });

            if (dto.PlanningEntryId.HasValue
                && !await _context.PlanningEntries.AnyAsync(pe => pe.Id == dto.PlanningEntryId.Value))
                return BadRequest(new { message = "Cellule de planning introuvable." });

            if (dto.TempsTheoriqueHeures < 0 || dto.TempsReelHeures < 0)
                return BadRequest(new { message = "Les temps ne peuvent pas être négatifs." });

            var etape = new OrdreFabricationEtape
            {
                OrdreFabricationId = dto.OrdreFabricationId,
                TypeEtape = typeEtape,
                Statut = statut,
                ChaineProductionId = dto.ChaineProductionId,
                PlanningEntryId = dto.PlanningEntryId,
                DateDebutPrevue = dto.DateDebutPrevue,
                DateFinPrevue = dto.DateFinPrevue,
                DateDebutReelle = dto.DateDebutReelle,
                DateFinReelle = dto.DateFinReelle,
                TempsTheoriqueHeures = dto.TempsTheoriqueHeures,
                TempsReelHeures = dto.TempsReelHeures,
                ResponsableAssigne = dto.ResponsableAssigne,
                Notes = dto.Notes,
                CreePar = User.Identity?.Name,
            };
            _context.OrdresFabricationEtapes.Add(etape);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Étape créée", id = etape.Id });
        }

        // ═══════════ Mise à jour ═══════════

        [HttpPut("{id}")]
        [RequireModulePermission("production", requireWrite: true)]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateOrdreFabricationEtapeDto dto)
        {
            var etape = await _context.OrdresFabricationEtapes.FindAsync(id);
            if (etape == null)
                return NotFound(new { message = "Étape d'OF introuvable." });

            if (dto.TypeEtape != null)
            {
                if (!Enum.TryParse<TypeEtapeProduction>(dto.TypeEtape, true, out var typeEtape))
                    return BadRequest(new { message = $"Type d'étape invalide : '{dto.TypeEtape}'." });
                etape.TypeEtape = typeEtape;
            }

            if (dto.Statut != null)
            {
                if (!Enum.TryParse<StatutTache>(dto.Statut, true, out var statut))
                    return BadRequest(new { message = $"Statut invalide : '{dto.Statut}'." });
                etape.Statut = statut;
            }

            if (dto.ChaineProductionId.HasValue)
            {
                if (dto.ChaineProductionId.Value != 0
                    && !await _context.ChainesProduction.AnyAsync(cp => cp.Id == dto.ChaineProductionId.Value))
                    return BadRequest(new { message = "Chaîne de production introuvable." });
                etape.ChaineProductionId = dto.ChaineProductionId.Value == 0 ? null : dto.ChaineProductionId.Value;
            }

            if (dto.PlanningEntryId.HasValue)
            {
                if (dto.PlanningEntryId.Value != 0
                    && !await _context.PlanningEntries.AnyAsync(pe => pe.Id == dto.PlanningEntryId.Value))
                    return BadRequest(new { message = "Cellule de planning introuvable." });
                etape.PlanningEntryId = dto.PlanningEntryId.Value == 0 ? null : dto.PlanningEntryId.Value;
            }

            if (dto.DateDebutPrevue.HasValue) etape.DateDebutPrevue = dto.DateDebutPrevue;
            if (dto.DateFinPrevue.HasValue) etape.DateFinPrevue = dto.DateFinPrevue;
            if (dto.DateDebutReelle.HasValue) etape.DateDebutReelle = dto.DateDebutReelle;
            if (dto.DateFinReelle.HasValue) etape.DateFinReelle = dto.DateFinReelle;
            if (dto.TempsTheoriqueHeures.HasValue)
            {
                if (dto.TempsTheoriqueHeures.Value < 0)
                    return BadRequest(new { message = "Les temps ne peuvent pas être négatifs." });
                etape.TempsTheoriqueHeures = dto.TempsTheoriqueHeures.Value;
            }
            if (dto.TempsReelHeures.HasValue)
            {
                if (dto.TempsReelHeures.Value < 0)
                    return BadRequest(new { message = "Les temps ne peuvent pas être négatifs." });
                etape.TempsReelHeures = dto.TempsReelHeures.Value;
            }
            if (dto.ResponsableAssigne != null) etape.ResponsableAssigne = dto.ResponsableAssigne;
            if (dto.Notes != null) etape.Notes = dto.Notes;

            etape.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Étape mise à jour", id = etape.Id });
        }

        // ═══════════ Suppression ═══════════

        [HttpDelete("{id}")]
        [RequireModulePermission("production", requireWrite: true)]
        public async Task<ActionResult> Delete(int id)
        {
            var etape = await _context.OrdresFabricationEtapes.FindAsync(id);
            if (etape == null)
                return NotFound(new { message = "Étape d'OF introuvable." });

            _context.OrdresFabricationEtapes.Remove(etape);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Étape supprimée" });
        }
    }
}