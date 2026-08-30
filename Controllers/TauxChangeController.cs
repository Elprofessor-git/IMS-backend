using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.TauxChange;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TauxChangeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TauxChangeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Référentiel des devises actives — consommé par les dropdowns de formulaire.
        // Dispo pour tout utilisateur authentifié (nécessaire pour créer achats/importations).
        [HttpGet("~/api/Devise")]
        public async Task<ActionResult<IEnumerable<Devise>>> GetDevisesActives()
        {
            return await _context.Devises
                .Where(d => d.EstActif)
                .OrderBy(d => d.Code)
                .ToListAsync();
        }

        // Liste des taux de change, filtrable par devise (réservé à l'administration).
        [HttpGet]
        [RequireModulePermission("parametres")]
        public async Task<ActionResult<IEnumerable<TauxChangeDto>>> GetTauxChanges([FromQuery] string? deviseCode)
        {
            var query = _context.TauxChanges
                .Include(t => t.Devise)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(deviseCode))
                query = query.Where(t => t.DeviseCode == deviseCode);

            return await query
                .OrderByDescending(t => t.DateEffective)
                .Select(t => new TauxChangeDto
                {
                    Id = t.Id,
                    DeviseCode = t.DeviseCode,
                    DeviseNom = t.Devise.Nom,
                    DeviseSymbole = t.Devise.Symbole,
                    DateEffective = t.DateEffective,
                    Taux = t.Taux
                })
                .ToListAsync();
        }

        // Saisie manuelle d'un taux (réservé à l'administration, module parametres).
        // Pas de PUT/DELETE : un nouveau taux avec une date plus récente prévaut.
        [HttpPost]
        [RequireModulePermission("parametres", requireWrite: true)]
        public async Task<ActionResult<TauxChangeDto>> CreateTauxChange([FromBody] CreateTauxChangeDto dto)
        {
            var deviseExiste = await _context.Devises.AnyAsync(d => d.Code == dto.DeviseCode);
            if (!deviseExiste)
                return BadRequest(new { message = $"Devise '{dto.DeviseCode}' inconnue." });

            // Aucun taux nécessaire pour la devise de référence TND (taux implicite = 1).
            if (dto.DeviseCode == "TND")
                return BadRequest(new { message = "La devise de référence TND n'a pas besoin de taux (taux implicite = 1)." });

            var taux = new TauxChange
            {
                DeviseCode = dto.DeviseCode,
                DateEffective = dto.DateEffective,
                Taux = dto.Taux
            };

            _context.TauxChanges.Add(taux);
            await _context.SaveChangesAsync();

            var result = await _context.TauxChanges
                .Include(t => t.Devise)
                .Where(t => t.Id == taux.Id)
                .Select(t => new TauxChangeDto
                {
                    Id = t.Id,
                    DeviseCode = t.DeviseCode,
                    DeviseNom = t.Devise.Nom,
                    DeviseSymbole = t.Devise.Symbole,
                    DateEffective = t.DateEffective,
                    Taux = t.Taux
                })
                .FirstAsync();

            return Ok(result);
        }
    }
}
