using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Référentiel global des chaînes de production (sous-traitance).
    /// Pas de suppression physique : les chaînes référencées par des
    /// LotExport/EnvoiFourniture sont désactivées via EstActif.
    /// L'enum <see cref="TypeChaineProduction"/> est stocké en string (règle projet).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChaineProductionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ChaineProductionController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<ChaineProductionDto>>> GetChaines()
        {
            var chaines = await _context.ChainesProduction
                .OrderBy(c => c.Id)
                .Select(c => new ChaineProductionDto
                {
                    Id = c.Id,
                    Nom = c.Nom,
                    TypeChaine = c.TypeChaine.ToString(),
                    EstActif = c.EstActif,
                    NombreEnvois = c.EnvoisFourniture.Count,
                    NombreExports = c.LotExports.Count,
                })
                .ToListAsync();
            return Ok(chaines);
        }

        [HttpGet("{id}")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<ChaineProductionDto>> GetChaine(int id)
        {
            var chaine = await _context.ChainesProduction
                .Where(c => c.Id == id)
                .Select(c => new ChaineProductionDto
                {
                    Id = c.Id,
                    Nom = c.Nom,
                    TypeChaine = c.TypeChaine.ToString(),
                    EstActif = c.EstActif,
                    NombreEnvois = c.EnvoisFourniture.Count,
                    NombreExports = c.LotExports.Count,
                })
                .FirstOrDefaultAsync();
            if (chaine == null)
                return NotFound(new { message = "Chaîne de production introuvable." });
            return Ok(chaine);
        }

        [HttpPost]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> CreateChaine([FromBody] CreateChaineProductionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nom))
                return BadRequest(new { message = "Le nom de la chaîne est requis." });

            TypeChaineProduction type;
            if (!Enum.TryParse<TypeChaineProduction>(dto.TypeChaine, out type))
                return BadRequest(new { message = $"Type de chaîne invalide : '{dto.TypeChaine}'." });

            if (await _context.ChainesProduction.AnyAsync(c => c.Nom == dto.Nom))
                return Conflict(new { message = $"Une chaîne nommée '{dto.Nom}' existe déjà." });

            var chaine = new ChaineProduction
            {
                Nom = dto.Nom,
                TypeChaine = type,
                EstActif = true,
            };
            _context.ChainesProduction.Add(chaine);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Chaîne de production créée", id = chaine.Id });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> UpdateChaine(int id, [FromBody] UpdateChaineProductionDto dto)
        {
            var chaine = await _context.ChainesProduction.FindAsync(id);
            if (chaine == null)
                return NotFound(new { message = "Chaîne de production introuvable." });

            if (dto.Nom != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Nom))
                    return BadRequest(new { message = "Le nom de la chaîne est requis." });
                if (await _context.ChainesProduction.AnyAsync(c => c.Nom == dto.Nom && c.Id != id))
                    return Conflict(new { message = $"Une chaîne nommée '{dto.Nom}' existe déjà." });
                chaine.Nom = dto.Nom;
            }

            if (dto.TypeChaine != null)
            {
                TypeChaineProduction type;
                if (!Enum.TryParse<TypeChaineProduction>(dto.TypeChaine, out type))
                    return BadRequest(new { message = $"Type de chaîne invalide : '{dto.TypeChaine}'." });
                chaine.TypeChaine = type;
            }

            if (dto.EstActif.HasValue)
                chaine.EstActif = dto.EstActif.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Chaîne de production mise à jour" });
        }

        /// <summary>
        /// Désactivation logique uniquement — la suppression physique est interdite
        /// par les FK Restrict/SetNull vers LotExports et EnvoisFourniture.
        /// </summary>
        [HttpDelete("{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> DesactiverChaine(int id)
        {
            var chaine = await _context.ChainesProduction.FindAsync(id);
            if (chaine == null)
                return NotFound(new { message = "Chaîne de production introuvable." });

            chaine.EstActif = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Chaîne de production désactivée" });
        }

        /// <summary>
        /// Réactivation logique — miroir de <see cref="DesactiverChaine"/>.
        /// PUT idempotent : réactiver une chaîne déjà active est sans effet.
        /// La chaîne réactivée réapparaît dans les colonnes du planning
        /// (GET /api/planning ne renvoie que les chaînes EstActif).
        /// </summary>
        [HttpPut("{id}/reactiver")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> ReactiverChaine(int id)
        {
            var chaine = await _context.ChainesProduction.FindAsync(id);
            if (chaine == null)
                return NotFound(new { message = "Chaîne de production introuvable." });

            chaine.EstActif = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Chaîne de production réactivée" });
        }
    }
}