using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
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

        // GET: api/MouvementStock
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MouvementStock>>> GetMouvements()
        {
            return await _context.MouvementsStock
                .Include(ms => ms.Stock)
                .ThenInclude(s => s.Article)
                .Include(ms => ms.TacheProduction)
                .OrderByDescending(ms => ms.DateMouvement)
                .Take(100) // Limiter à 100 derniers mouvements par défaut
                .ToListAsync();
        }

        // GET: api/MouvementStock/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MouvementStock>> GetMouvementStock(int id)
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

            return mouvement;
        }

        // GET: api/MouvementStock/ByStock/5
        [HttpGet("ByStock/{stockId}")]
        public async Task<ActionResult<IEnumerable<MouvementStock>>> GetMouvementsByStock(int stockId)
        {
            return await _context.MouvementsStock
                .Include(ms => ms.TacheProduction)
                .Where(ms => ms.StockId == stockId)
                .OrderByDescending(ms => ms.DateMouvement)
                .ToListAsync();
        }

        // GET: api/MouvementStock/ByArticle/5
        [HttpGet("ByArticle/{articleId}")]
        public async Task<ActionResult<IEnumerable<MouvementStock>>> GetMouvementsByArticle(int articleId)
        {
            return await _context.MouvementsStock
                .Include(ms => ms.Stock)
                .ThenInclude(s => s.Article)
                .Include(ms => ms.TacheProduction)
                .Where(ms => ms.Stock.ArticleId == articleId)
                .OrderByDescending(ms => ms.DateMouvement)
                .ToListAsync();
        }

        // GET: api/MouvementStock/Filtrer
        [HttpGet("Filtrer")]
        public async Task<ActionResult<IEnumerable<MouvementStock>>> FiltrerMouvements(
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

            return await query
                .OrderByDescending(ms => ms.DateMouvement)
                .Take(500) // Limiter les résultats
                .ToListAsync();
        }

        // GET: api/MouvementStock/Statistiques
        [HttpGet("Statistiques")]
        public async Task<ActionResult<object>> GetStatistiques(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin)
        {
            var debut = dateDebut ?? DateTime.Now.AddDays(-30);
            var fin = dateFin ?? DateTime.Now;

            var mouvements = await _context.MouvementsStock
                .Where(ms => ms.DateMouvement >= debut && ms.DateMouvement <= fin)
                .ToListAsync();

            var statistiques = new
            {
                Periode = new { DateDebut = debut, DateFin = fin },
                TotalMouvements = mouvements.Count,
                Entrees = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Entree),
                Sorties = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Sortie),
                Transferts = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Transfert),
                Ajustements = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Ajustement),
                Reservations = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Reservation),
                Liberations = mouvements.Count(m => m.TypeMouvement == TypeMouvement.Liberation),
                QuantiteTotaleEntree = mouvements
                    .Where(m => m.TypeMouvement == TypeMouvement.Entree)
                    .Sum(m => m.Quantite),
                QuantiteTotaleSortie = mouvements
                    .Where(m => m.TypeMouvement == TypeMouvement.Sortie)
                    .Sum(m => m.Quantite),
                MouvementsParOrigine = mouvements
                    .GroupBy(m => m.OrigineMouvement)
                    .Select(g => new { Origine = g.Key.ToString(), Nombre = g.Count() })
                    .ToList(),
                MouvementsParJour = mouvements
                    .GroupBy(m => m.DateMouvement.Date)
                    .Select(g => new { Date = g.Key, Nombre = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToList()
            };

            return Ok(statistiques);
        }

        // POST: api/MouvementStock
        [HttpPost]
        public async Task<ActionResult<MouvementStock>> PostMouvementStock(MouvementStock mouvement)
        {
            // Vérifier que le stock existe
            var stock = await _context.Stocks.FindAsync(mouvement.StockId);
            if (stock == null)
            {
                return BadRequest("Stock introuvable");
            }

            // Calculer les quantités avant et après
            mouvement.QuantiteAvant = stock.Quantite;
            
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
                    mouvement.QuantiteApres = mouvement.Quantite; // La quantité finale souhaitée
                    stock.Quantite = mouvement.Quantite;
                    mouvement.Quantite = Math.Abs(mouvement.QuantiteApres - mouvement.QuantiteAvant);
                    break;
                    
                case TypeMouvement.Transfert:
                    // Pour les transferts, la logique dépend de l'implémentation spécifique
                    mouvement.QuantiteApres = stock.Quantite;
                    break;
            }

            mouvement.DateMouvement = DateTime.Now;
            
            _context.MouvementsStock.Add(mouvement);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMouvementStock", new { id = mouvement.Id }, mouvement);
        }

        // POST: api/MouvementStock/Transfert
        [HttpPost("Transfert")]
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

            // Mouvement de sortie du stock source
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

            // Mouvement d'entrée dans le stock destination
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

            // Mettre à jour les quantités
            stockSource.Quantite -= request.Quantite;
            stockDestination.Quantite += request.Quantite;

            _context.MouvementsStock.AddRange(mouvementSortie, mouvementEntree);
            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Transfert effectué avec succès",
                mouvementSortieId = mouvementSortie.Id,
                mouvementEntreeId = mouvementEntree.Id
            });
        }

        // DELETE: api/MouvementStock/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMouvementStock(int id)
        {
            var mouvement = await _context.MouvementsStock.FindAsync(id);
            if (mouvement == null)
            {
                return NotFound();
            }

            // Vérifier si le mouvement peut être supprimé (par exemple, pas trop ancien)
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
    }

    // Classe pour les requêtes de transfert
    public class TransfertRequest
    {
        public int StockSourceId { get; set; }
        public int StockDestinationId { get; set; }
        public decimal Quantite { get; set; }
        public string Motif { get; set; } = string.Empty;
        public string EffectuePar { get; set; } = string.Empty;
    }
}

