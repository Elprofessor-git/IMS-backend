using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Mouvement;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MouvementStockController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MouvementStockController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static MouvementStockDto ToDto(MouvementStock ms)
        {
            return new MouvementStockDto
            {
                Id = ms.Id,
                StockId = ms.StockId,
                ArticleId = ms.Stock?.ArticleId ?? 0,
                TypeMouvement = ms.TypeMouvement,
                OrigineMouvement = ms.OrigineMouvement,
                Quantite = ms.Quantite,
                StockAvant = ms.QuantiteAvant,
                StockApres = ms.QuantiteApres,
                Motif = ms.Motif,
                NumeroReference = ms.NumeroLot,
                EmplacementSource = ms.EmplacementSource,
                EmplacementDestination = ms.EmplacementDestination,
                EffectuePar = ms.EffectuePar,
                DateMouvement = ms.DateMouvement,
                Article = ms.Stock?.Article == null
                    ? null
                    : new MouvementArticleDto
                    {
                        Id = ms.Stock.Article.Id,
                        Designation = ms.Stock.Article.Designation,
                        Reference = ms.Stock.Article.Reference
                    }
            };
        }

        private static List<MouvementStockDto> ToDtoList(IEnumerable<MouvementStock> mouvements)
        {
            return mouvements.Select(ToDto).ToList();
        }

        [HttpGet]
        [RequireModulePermission("mouvements", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<MouvementStockDto>>> GetMouvements()
        {
            var mouvements = await _context.MouvementsStock
                .Include(ms => ms.Stock)
                .ThenInclude(s => s.Article)
                .Include(ms => ms.TacheProduction)
                .OrderByDescending(ms => ms.DateMouvement)
                .Take(100)
                .ToListAsync();

            return Ok(ToDtoList(mouvements));
        }

        [HttpGet("{id}")]
        [RequireModulePermission("mouvements", requireWrite: false)]
        public async Task<ActionResult<MouvementStockDto>> GetMouvementStock(int id)
        {
            var mouvement = await _context.MouvementsStock
                .Include(ms => ms.Stock)
                .ThenInclude(s => s.Article)
                .Include(ms => ms.TacheProduction)
                .FirstOrDefaultAsync(ms => ms.Id == id);

            if (mouvement == null)
            {
                return NotFound();
            }

            return Ok(ToDto(mouvement));
        }

        [HttpGet("ByStock/{stockId}")]
        [RequireModulePermission("mouvements", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<MouvementStockDto>>> GetMouvementsByStock(int stockId)
        {
            var mouvements = await _context.MouvementsStock
                .Include(ms => ms.Stock)
                .ThenInclude(s => s.Article)
                .Include(ms => ms.TacheProduction)
                .Where(ms => ms.StockId == stockId)
                .OrderByDescending(ms => ms.DateMouvement)
                .ToListAsync();

            return Ok(ToDtoList(mouvements));
        }

        [HttpGet("ByArticle/{articleId}")]
        [RequireModulePermission("mouvements", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<MouvementStockDto>>> GetMouvementsByArticle(int articleId)
        {
            var mouvements = await _context.MouvementsStock
                .Include(ms => ms.Stock)
                .ThenInclude(s => s.Article)
                .Include(ms => ms.TacheProduction)
                .Where(ms => ms.Stock.ArticleId == articleId)
                .OrderByDescending(ms => ms.DateMouvement)
                .ToListAsync();

            return Ok(ToDtoList(mouvements));
        }

        [HttpGet("Filtrer")]
        [RequireModulePermission("mouvements", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<MouvementStockDto>>> FiltrerMouvements(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] TypeMouvement? typeMouvement,
            [FromQuery] OrigineMouvement? origineMouvement,
            [FromQuery] int? articleId,
            [FromQuery] string? effectuePar)
        {
            var query = _context.MouvementsStock
                .Include(ms => ms.Stock)
                .ThenInclude(s => s.Article)
                .Include(ms => ms.TacheProduction)
                .AsQueryable();

            if (dateDebut.HasValue)
                query = query.Where(ms => ms.DateMouvement >= dateDebut.Value);

            if (dateFin.HasValue)
                query = query.Where(ms => ms.DateMouvement <= dateFin.Value);

            if (typeMouvement.HasValue)
                query = query.Where(ms => ms.TypeMouvement == typeMouvement.Value);

            if (origineMouvement.HasValue)
                query = query.Where(ms => ms.OrigineMouvement == origineMouvement.Value);

            if (articleId.HasValue)
                query = query.Where(ms => ms.Stock.ArticleId == articleId.Value);

            if (!string.IsNullOrEmpty(effectuePar))
                query = query.Where(ms => ms.EffectuePar.Contains(effectuePar));

            var mouvements = await query
                .OrderByDescending(ms => ms.DateMouvement)
                .Take(500)
                .ToListAsync();

            return Ok(ToDtoList(mouvements));
        }

        [HttpGet("Statistiques")]
        [RequireModulePermission("mouvements", requireWrite: false)]
        public async Task<ActionResult<MouvementStatistiquesDto>> GetStatistiques(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin)
        {
            // Cohérent avec FiltrerMouvements : pas de fenêtre par défaut.
            // (La liste des mouvements n'applique la date que si le filtre est fourni.)
            var query = _context.MouvementsStock.AsQueryable();

            if (dateDebut.HasValue)
                query = query.Where(ms => ms.DateMouvement >= dateDebut.Value);

            if (dateFin.HasValue)
                query = query.Where(ms => ms.DateMouvement <= dateFin.Value);

            var mouvements = await query.ToListAsync();

            var valeurStock = await _context.Stocks.SumAsync(s => s.Quantite * s.PrixUnitaire);

            var statistiques = new MouvementStatistiquesDto
            {
                TotalMouvements = mouvements.Count,
                TotalEntrees = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Entree),
                TotalSorties = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Sortie),
                TotalTransferts = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Transfert),
                TotalAjustements = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Ajustement),
                TotalReservations = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Reservation),
                TotalLiberations = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Liberation),
                QuantiteTotaleEntree = mouvements
                    .Where(m => m.TypeMouvement == TypeMouvement.Entree)
                    .Sum(m => m.Quantite),
                QuantiteTotaleSortie = mouvements
                    .Where(m => m.TypeMouvement == TypeMouvement.Sortie)
                    .Sum(m => m.Quantite),
                StockValeurTotale = valeurStock,
                MouvementsParOrigine = mouvements
                    .GroupBy(m => m.OrigineMouvement)
                    .Select(g => new MouvementParOrigineDto { Origine = g.Key.ToString(), Nombre = g.Count() })
                    .ToList()
            };

            return Ok(statistiques);
        }

        [HttpPost]
        [RequireModulePermission("mouvements", requireWrite: true)]
        public async Task<ActionResult<MouvementStock>> PostMouvementStock(CreateMouvementStockDto dto)
        {
            var stock = await _context.Stocks.FindAsync(dto.StockId);
            if (stock == null)
            {
                return BadRequest("Stock introuvable");
            }

            var mouvement = new MouvementStock
            {
                StockId = dto.StockId,
                TypeMouvement = dto.TypeMouvement,
                OrigineMouvement = dto.OrigineMouvement,
                Quantite = dto.Quantite,
                EmplacementSource = dto.EmplacementSource,
                EmplacementDestination = dto.EmplacementDestination,
                NumeroLot = dto.NumeroReference,
                Motif = dto.Motif,
                EffectuePar = dto.EffectuePar,
                QuantiteAvant = stock.Quantite
            };

            switch (mouvement.TypeMouvement)
            {
                case TypeMouvement.Entree:
                    mouvement.QuantiteApres = stock.Quantite + mouvement.Quantite;
                    stock.Quantite += mouvement.Quantite;
                    break;

                case TypeMouvement.Sortie:
                    if (stock.Quantite < mouvement.Quantite)
                    {
                        return BadRequest("Quantité insuffisante en stock");
                    }
                    mouvement.QuantiteApres = stock.Quantite - mouvement.Quantite;
                    stock.Quantite -= mouvement.Quantite;
                    break;

                case TypeMouvement.Reservation:
                    if (stock.Quantite - stock.QuantiteReservee < mouvement.Quantite)
                    {
                        return BadRequest("Quantité insuffisante disponible pour réservation");
                    }
                    stock.QuantiteReservee += mouvement.Quantite;
                    mouvement.QuantiteApres = stock.Quantite;
                    break;

                case TypeMouvement.Liberation:
                    if (stock.QuantiteReservee < mouvement.Quantite)
                    {
                        return BadRequest("Quantité réservée insuffisante pour libération");
                    }
                    stock.QuantiteReservee -= mouvement.Quantite;
                    mouvement.QuantiteApres = stock.Quantite;
                    break;

                case TypeMouvement.Ajustement:
                    mouvement.QuantiteApres = mouvement.Quantite;
                    stock.Quantite = mouvement.Quantite;
                    mouvement.Quantite = Math.Abs(mouvement.QuantiteApres - mouvement.QuantiteAvant);
                    break;

                case TypeMouvement.Transfert:
                    mouvement.QuantiteApres = stock.Quantite;
                    break;
            }

            mouvement.DateMouvement = DateTime.Now;

            _context.MouvementsStock.Add(mouvement);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMouvementStock", new { id = mouvement.Id }, mouvement);
        }

        [HttpPost("Transfert")]
        [RequireModulePermission("mouvements", requireWrite: true)]
        public async Task<ActionResult> EffectuerTransfert([FromBody] TransfertRequest request)
        {
            var stockSource = await _context.Stocks.FindAsync(request.StockSourceId);
            var stockDestination = await _context.Stocks.FindAsync(request.StockDestinationId);

            if (stockSource == null || stockDestination == null)
            {
                return BadRequest("Stock source ou destination introuvable");
            }

            if (stockSource.Quantite < request.Quantite)
            {
                return BadRequest("Quantité insuffisante dans le stock source");
            }

            var mouvementSortie = new MouvementStock
            {
                StockId = request.StockSourceId,
                TypeMouvement = TypeMouvement.Transfert,
                OrigineMouvement = OrigineMouvement.Transfert,
                Quantite = request.Quantite,
                QuantiteAvant = stockSource.Quantite,
                QuantiteApres = stockSource.Quantite - request.Quantite,
                EmplacementSource = stockSource.EmplacementPhysique,
                EmplacementDestination = stockDestination.EmplacementPhysique,
                Motif = request.Motif,
                DateMouvement = DateTime.Now,
                EffectuePar = request.EffectuePar
            };

            var mouvementEntree = new MouvementStock
            {
                StockId = request.StockDestinationId,
                TypeMouvement = TypeMouvement.Transfert,
                OrigineMouvement = OrigineMouvement.Transfert,
                Quantite = request.Quantite,
                QuantiteAvant = stockDestination.Quantite,
                QuantiteApres = stockDestination.Quantite + request.Quantite,
                EmplacementSource = stockSource.EmplacementPhysique,
                EmplacementDestination = stockDestination.EmplacementPhysique,
                Motif = request.Motif,
                DateMouvement = DateTime.Now,
                EffectuePar = request.EffectuePar
            };

            stockSource.Quantite -= request.Quantite;
            stockDestination.Quantite += request.Quantite;

            _context.MouvementsStock.AddRange(mouvementSortie, mouvementEntree);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Transfert effectué avec succès",
                mouvementSortieId = mouvementSortie.Id,
                mouvementEntreeId = mouvementEntree.Id
            });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("mouvements", requireWrite: true)]
        public async Task<IActionResult> DeleteMouvementStock(int id)
        {
            var mouvement = await _context.MouvementsStock.FindAsync(id);
            if (mouvement == null)
            {
                return NotFound();
            }

            if (mouvement.DateMouvement < DateTime.Now.AddDays(-7))
            {
                return BadRequest("Impossible de supprimer un mouvement de plus de 7 jours");
            }

            _context.MouvementsStock.Remove(mouvement);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MouvementStockExists(int id)
        {
            return _context.MouvementsStock.Any(e => e.Id == id);
        }

        /// <summary>
        /// LOT 3 : Libération physique — rend un Stock scopé exclusif à une commande vers
        /// son scope partagé d'origine (Client/Plateforme/Groupe). Le scope d'origine est
        /// conservé sur la ligne scindée (LOT 2.3) et restauré ici.
        /// </summary>
        [HttpPost("LibererPhysique")]
        [RequireModulePermission("stock", requireWrite: true)]
        public async Task<IActionResult> LibererPhysique([FromBody] LibererStockPhysiqueDto dto)
        {
            var stock = await _context.Stocks.FindAsync(dto.StockId);
            if (stock == null)
                return NotFound("Stock introuvable.");

            if (stock.CommandeClientId == null)
                return BadRequest("Ce stock n'est pas exclusif à une commande.");

            if (stock.Quantite < dto.Quantite)
                return BadRequest("Quantité insuffisante.");

            var stockId = stock.Id;
            stock.Quantite -= dto.Quantite;

            // LOT 3 : restaurer le scope d'origine (conservé sur la ligne scindée par LOT 2.3)
            _context.Stocks.Add(new Stock
            {
                ArticleId = stock.ArticleId,
                TypeStock = stock.TypeStock,
                Quantite = dto.Quantite,
                ClientId = stock.ClientId,
                PlateformeId = stock.PlateformeId,
                GroupeCommandeId = stock.GroupeCommandeId,
                PrixUnitaire = stock.PrixUnitaire,
                Devise = stock.Devise,
                DateEntree = stock.DateEntree,
                EstValide = stock.EstValide,
                ValidePar = stock.ValidePar
            });

            _context.MouvementsStock.Add(new MouvementStock
            {
                StockId = stockId,
                TypeMouvement = TypeMouvement.Transfert,
                Quantite = dto.Quantite,
                DateMouvement = DateTime.Now,
                Notes = $"Libération physique exclusif commande #{stock.CommandeClientId} → scope partagé"
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Stock libéré avec succès." });
        }
    }

    public class TransfertRequest
    {
        public int StockSourceId { get; set; }
        public int StockDestinationId { get; set; }
        public decimal Quantite { get; set; }
        public string Motif { get; set; } = string.Empty;
        public string EffectuePar { get; set; } = string.Empty;
    }

    public class LibererStockPhysiqueDto
    {
        public int StockId { get; set; }
        public decimal Quantite { get; set; }
        public int? ClientId { get; set; }
        public int? PlateformeId { get; set; }
        public int? GroupeCommandeId { get; set; }
    }
}
