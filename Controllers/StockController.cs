using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StockController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StockController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [RequireModulePermission("stock", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Stock>>> GetStocks()
        {
            return await _context.Stocks
                .Include(s => s.Article)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireModulePermission("stock", requireWrite: false)]
        public async Task<ActionResult<Stock>> GetStock(int id)
        {
            var stock = await _context.Stocks
                .Include(s => s.Article)
                .Include(s => s.Mouvements)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (stock == null)
            {
                return NotFound();
            }

            return stock;
        }

        [HttpGet("ByArticle/{articleId}")]
        [RequireModulePermission("stock", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Stock>>> GetStocksByArticle(int articleId)
        {
            return await _context.Stocks
                .Include(s => s.Article)
                .Where(s => s.ArticleId == articleId)
                .ToListAsync();
        }

        [HttpGet("Libre")]
        [RequireModulePermission("stock", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Stock>>> GetStocksLibres()
        {
            return await _context.Stocks
                .Include(s => s.Article)
                .Where(s => s.TypeStock == TypeStock.Libre && s.Quantite > 0)
                .ToListAsync();
        }

        [HttpGet("Reserve")]
        [RequireModulePermission("stock", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<Stock>>> GetStocksReserves()
        {
            return await _context.Stocks
                .Include(s => s.Article)
                .Include(s => s.CommandeClient)
                .Where(s => s.TypeStock == TypeStock.Reserve || s.QuantiteReservee > 0)
                .ToListAsync();
        }

        [HttpGet("Alertes")]
        [RequireModulePermission("stock", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<object>>> GetStocksAlertes()
        {
            var alertes = await _context.Stocks
                .Include(s => s.Article)
                .Where(s => s.Quantite <= s.Article.SeuilAlerte)
                .Select(s => new
                {
                    s.Id,
                    s.Article.Designation,
                    s.Quantite,
                    SeuilAlerte = s.Article.SeuilAlerte,
                    SeuilCritique = s.Article.SeuilCritique,
                    EstCritique = s.Quantite <= s.Article.SeuilCritique
                })
                .ToListAsync();

            return Ok(alertes);
        }

        [HttpGet("Emplacements")]
        [RequireModulePermission("stock", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<string>>> GetEmplacements()
        {
            var emplacements = await _context.Stocks
                .Where(s => s.EmplacementPhysique != null && s.EmplacementPhysique != "")
                .Select(s => s.EmplacementPhysique!)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();
            return Ok(emplacements);
        }

        [HttpPost]
        [RequireModulePermission("stock", requireWrite: true)]
        public async Task<ActionResult<Stock>> PostStock(Stock stock)
        {
            stock.DateEntree = DateTime.Now;
            _context.Stocks.Add(stock);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetStock", new { id = stock.Id }, stock);
        }

        [HttpPut("{id}")]
        [RequireModulePermission("stock", requireWrite: true)]
        public async Task<IActionResult> PutStock(int id, Stock stock)
        {
            if (id != stock.Id)
            {
                return BadRequest();
            }

            _context.Entry(stock).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockExists(id))
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

        [HttpPost("{id}/Valider")]
        [RequireModulePermission("stock", requireWrite: true)]
        public async Task<IActionResult> ValiderStock(int id, [FromBody] string validePar)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null)
            {
                return NotFound();
            }

            stock.EstValide = true;
            stock.ValidePar = validePar;
            stock.DateValidation = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Stock validé avec succès" });
        }

        [HttpPost("{id}/Reserver")]
        [RequireModulePermission("stock", requireWrite: true)]
        public async Task<IActionResult> ReserverStock(int id, [FromBody] decimal quantite)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null)
            {
                return NotFound();
            }

            if (stock.Quantite < quantite)
            {
                return BadRequest("Quantité insuffisante en stock");
            }

            stock.QuantiteReservee += quantite;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Stock réservé avec succès" });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("stock", requireWrite: true)]
        public async Task<IActionResult> DeleteStock(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null)
            {
                return NotFound();
            }

            _context.Stocks.Remove(stock);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool StockExists(int id)
        {
            return _context.Stocks.Any(e => e.Id == id);
        }
    }
}
