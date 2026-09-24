using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Qualite;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Référentiel des codes défauts (conception v1.1 §4.2).
    /// Permissions : module « qualite » (Décision 6).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DefautCodeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DefautCodeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<DefautCodeDto>>> GetDefauts()
        {
            var defauts = await _context.DefautCodes
                .OrderBy(d => d.Code)
                .Select(d => new DefautCodeDto
                {
                    Id = d.Id,
                    Code = d.Code,
                    Libelle = d.Libelle,
                    EstActif = d.EstActif,
                })
                .ToListAsync();
            return Ok(defauts);
        }

        [HttpGet("{id}")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<DefautCodeDto>> GetDefaut(int id)
        {
            var defaut = await _context.DefautCodes.FindAsync(id);
            if (defaut == null)
                return NotFound(new { message = "Code défaut introuvable." });
            return Ok(new DefautCodeDto
            {
                Id = defaut.Id,
                Code = defaut.Code,
                Libelle = defaut.Libelle,
                EstActif = defaut.EstActif,
            });
        }

        [HttpPost]
        [RequireModulePermission("qualite", requireWrite: true)]
        public async Task<ActionResult> CreateDefaut([FromBody] CreateDefautCodeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Libelle))
                return BadRequest(new { message = "Code et libellé sont requis." });

            if (await _context.DefautCodes.AnyAsync(d => d.Code == dto.Code.Trim()))
                return Conflict(new { message = $"Le code défaut '{dto.Code}' existe déjà." });

            var defaut = new DefautCode
            {
                Code = dto.Code.Trim(),
                Libelle = dto.Libelle.Trim(),
                EstActif = dto.EstActif,
            };
            _context.DefautCodes.Add(defaut);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Code défaut créé", id = defaut.Id });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("qualite", requireWrite: true)]
        public async Task<ActionResult> UpdateDefaut(int id, [FromBody] UpdateDefautCodeDto dto)
        {
            var defaut = await _context.DefautCodes.FindAsync(id);
            if (defaut == null)
                return NotFound(new { message = "Code défaut introuvable." });

            if (dto.Code != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Code))
                    return BadRequest(new { message = "Le code est requis." });
                if (await _context.DefautCodes.AnyAsync(d => d.Code == dto.Code.Trim() && d.Id != id))
                    return Conflict(new { message = $"Le code défaut '{dto.Code}' existe déjà." });
                defaut.Code = dto.Code.Trim();
            }
            if (dto.Libelle != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Libelle))
                    return BadRequest(new { message = "Le libellé est requis." });
                defaut.Libelle = dto.Libelle.Trim();
            }
            if (dto.EstActif.HasValue)
                defaut.EstActif = dto.EstActif.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Code défaut mis à jour" });
        }

        /// <summary>Désactivation logique — la suppression physique est interdite
        /// par la FK Restrict vers ControleQualiteDefautLigne (audit trail).</summary>
        [HttpDelete("{id}")]
        [RequireModulePermission("qualite", requireWrite: true)]
        public async Task<ActionResult> DeleteDefaut(int id)
        {
            var defaut = await _context.DefautCodes.FindAsync(id);
            if (defaut == null)
                return NotFound(new { message = "Code défaut introuvable." });

            if (await _context.ControleQualiteDefautLignes.AnyAsync(l => l.DefautCodeId == id))
            {
                defaut.EstActif = false;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Code défaut désactivé (déjà utilisé dans l'historique)" });
            }

            _context.DefautCodes.Remove(defaut);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Code défaut supprimé" });
        }
    }
}