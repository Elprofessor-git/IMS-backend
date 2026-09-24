using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Dtos.Qualite;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Renvoi de retouche vers la chaîne source (conception §4.2, §4.4).
    /// Plafond strict : Σ QuantiteRenvoyee(ER d'un contrôle) ≤ QuantiteRetouche(contrôle)
    /// — SANS ForcerDepassement (refus en dur, 409) : renvoyer plus que la retouche
    /// déclarée ferait entrer au cycle des pièces sans origine (noir un seul cas
    /// où la « forcer » est légitime, contrairement aux plafonds physiques
    /// EnvoiFourniture/LotExport qui tolèrent un dépassement réel).
    /// Garde : EnvoiRetouche.ChaineProductionId = ControleQualite(parent).ChaineProductionId.
    /// Permissions : module « qualite ».
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EnvoiRetoucheController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly QualiteService _qualite;

        public EnvoiRetoucheController(ApplicationDbContext context, QualiteService quality)
        {
            _context = context;
            _qualite = quality;
        }

        [HttpGet]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<EnvoiRetoucheDto>>> List([FromQuery] int? controleQualiteId)
        {
            var query = _context.EnvoisRetouche.AsQueryable();
            if (controleQualiteId.HasValue)
                query = query.Where(e => e.ControleQualiteId == controleQualiteId.Value);

            var result = await query
                .OrderByDescending(e => e.DateEnvoi)
                .Select(e => new EnvoiRetoucheDto
                {
                    Id = e.Id,
                    ControleQualiteId = e.ControleQualiteId,
                    ChaineProductionId = e.ChaineProductionId,
                    ChaineNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    QuantiteRenvoyee = e.QuantiteRenvoyee,
                    DateEnvoi = e.DateEnvoi,
                    EffectuePar = e.EffectuePar,
                    Notes = e.Notes,
                })
                .ToListAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<EnvoiRetoucheDto>> Get(int id)
        {
            var result = await _context.EnvoisRetouche
                .Where(e => e.Id == id)
                .Select(e => new EnvoiRetoucheDto
                {
                    Id = e.Id,
                    ControleQualiteId = e.ControleQualiteId,
                    ChaineProductionId = e.ChaineProductionId,
                    ChaineNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    QuantiteRenvoyee = e.QuantiteRenvoyee,
                    DateEnvoi = e.DateEnvoi,
                    EffectuePar = e.EffectuePar,
                    Notes = e.Notes,
                })
                .FirstOrDefaultAsync();
            if (result == null)
                return NotFound(new { message = "Envoi de retouche introuvable." });
            return Ok(result);
        }

        [HttpPost]
        [RequireModulePermission("qualite", requireWrite: true)]
        public async Task<ActionResult> CreateEnvoi([FromBody] CreateEnvoiRetoucheDto dto)
        {
            var controle = await _context.ControlesQualite.FindAsync(dto.ControleQualiteId);
            if (controle == null)
                return NotFound(new { message = "Contrôle qualité source introuvable." });

            if (dto.QuantiteRenvoyee <= 0)
                return BadRequest(new { message = "La quantité renvoyée doit être > 0." });

            if (!await _context.ChainesProduction.AnyAsync(cp => cp.Id == dto.ChaineProductionId))
                return BadRequest(new { message = "Chaîne de production introuvable." });

            // Garde 4.4 : le renvoi part vers SA chaîne source (pas d'autre chaîne).
            if (dto.ChaineProductionId != controle.ChaineProductionId)
                return BadRequest(new { message = "L'envoi de retouche doit partir vers la chaîne souche du contrôle (garde d'intégrité)." });

            // Plafond strict SANS ForcerDepassement : Σ envois(contrôle) ≤ QuantiteRetouche(contrôle).
            var totalEnvois = await _context.EnvoisRetouche
                .Where(e => e.ControleQualiteId == controle.Id)
                .SumAsync(e => (int?)e.QuantiteRenvoyee) ?? 0;

            var plafond = controle.QuantiteRetouche;
            if (totalEnvois + dto.QuantiteRenvoyee > plafond)
                return Conflict(new
                {
                    message = $"Renvoi strictement plafonné : retouche déclarée = {plafond}, déjà renvoyé = {totalEnvois}, demandé = {dto.QuantiteRenvoyee}. Aucun ForcerDepassement n'existe sur ce plafond.",
                    plafond = plafond,
                    dejaRenvoye = totalEnvois,
                });

            var envoi = new EnvoiRetouche
            {
                ControleQualiteId = controle.Id,
                ChaineProductionId = dto.ChaineProductionId,
                QuantiteRenvoyee = dto.QuantiteRenvoyee,
                DateEnvoi = dto.DateEnvoi ?? DateTime.Now,
                EffectuePar = dto.EffectuePar ?? User.Identity?.Name,
                Notes = dto.Notes,
            };
            _context.EnvoisRetouche.Add(envoi);
            await _context.SaveChangesAsync();

            // Clôture comptable éventuelle (ne peut pas clôturer, mais cohérence).
            if (controle.OrdreFabricationId.HasValue)
            {
                var of = await _context.OrdresFabrication.FindAsync(controle.OrdreFabricationId.Value);
                if (of != null)
                    await _qualite.CloturerCommandeSiSoldee(of.CommandeId);
            }

            return Ok(new { message = "Retouche transmise au sous-traitant", id = envoi.Id, plafondRestant = plafond - totalEnvois - dto.QuantiteRenvoyee });
        }
    }
}