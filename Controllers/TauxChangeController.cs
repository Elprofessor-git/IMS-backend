using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.TauxChange;
using Backend_Gestion_Magasin_API.Services;
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

        // TEMPORAIRE (Phase 4 — chantier multi-devises) : backfill des montants convertis
        // en TND pour l'historique existant. À retirer après validation par Sof.
        // Réutilise TauxChangeService (règle : taux le plus proche en date à la date du
        // document ; TND => 1 ; aucune devise sans taux => 1 = limitation connue).
        [HttpPost("backfill")]
        [RequireModulePermission("parametres", requireWrite: true)]
        public async Task<ActionResult> BackfillMontantsTND()
        {
            var nbAchats = 0;
            var nbLignesAchat = 0;
            var nbImportations = 0;
            var nbLignesImportation = 0;
            var nbStocks = 0;

            var achats = await _context.Achats.Include(a => a.LignesAchat).ToListAsync();
            foreach (var achat in achats)
            {
                foreach (var ligne in achat.LignesAchat)
                {
                    var taux = await TauxChangeService.ObtenirTauxAsync(_context, ligne.Devise, achat.DateAchat);
                    ligne.MontantLigneTND = ligne.MontantLigne * taux;
                    nbLignesAchat++;
                }
                achat.MontantTotalTND = achat.LignesAchat.Sum(la => la.MontantLigneTND);
                nbAchats++;
            }

            var importations = await _context.Importations.Include(i => i.LignesImportation).ToListAsync();
            foreach (var importation in importations)
            {
                foreach (var ligne in importation.LignesImportation)
                {
                    var taux = await TauxChangeService.ObtenirTauxAsync(_context, ligne.Devise, importation.DateImportation);
                    ligne.MontantLigneTND = ligne.MontantLigne * taux;
                    nbLignesImportation++;
                }
                importation.MontantTotalTND = importation.LignesImportation.Sum(li => li.MontantLigneTND);
                nbImportations++;
            }

            var stocks = await _context.Stocks.ToListAsync();
            foreach (var stock in stocks)
            {
                var taux = await TauxChangeService.ObtenirTauxAsync(_context, stock.Devise, stock.DateEntree);
                stock.PrixUnitaireTND = stock.PrixUnitaire * taux;
                nbStocks++;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Backfill montants TND effectué.",
                nbAchats,
                nbLignesAchat,
                nbImportations,
                nbLignesImportation,
                nbStocks
            });
        }
    }
}
