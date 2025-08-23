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
    public class ArticleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ArticleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Article
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Article>>> GetArticles()
        {
            return await _context.Articles
                .Where(a => a.EstActif)
                .Include(a => a.Stocks)
                .ToListAsync();
        }

        // GET: api/Article/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Article>> GetArticle(int id)
        {
            var article = await _context.Articles
                .Include(a => a.Stocks)
                .Include(a => a.BesoinsCommande)
                .Include(a => a.LignesAchat)
                .Include(a => a.LignesImportation)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article == null)
            {
                return NotFound();
            }

            return article;
        }

        // GET: api/Article/Search/{terme}
        [HttpGet("Search/{terme}")]
        public async Task<ActionResult<IEnumerable<Article>>> SearchArticles(string terme)
        {
            return await _context.Articles
                .Where(a => a.EstActif && 
                           (a.Designation.Contains(terme) || 
                            a.Description.Contains(terme) ||
                            a.Reference.Contains(terme) ||
                            a.Categorie.Contains(terme)))
                .Include(a => a.Stocks)
                .ToListAsync();
        }

        // GET: api/Article/ByCategorie/{categorie}
        [HttpGet("ByCategorie/{categorie}")]
        public async Task<ActionResult<IEnumerable<Article>>> GetArticlesByCategorie(string categorie)
        {
            return await _context.Articles
                .Where(a => a.EstActif && a.Categorie == categorie)
                .Include(a => a.Stocks)
                .ToListAsync();
        }

        // GET: api/Article/5/StockTotal
        [HttpGet("{id}/StockTotal")]
        public async Task<ActionResult<object>> GetStockTotal(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            var stockInfo = await _context.Stocks
                .Where(s => s.ArticleId == id)
                .GroupBy(s => s.ArticleId)
                .Select(g => new
                {
                    ArticleId = g.Key,
                    QuantiteTotale = g.Sum(s => s.Quantite),
                    QuantiteReservee = g.Sum(s => s.QuantiteReservee),
                    QuantiteDisponible = g.Sum(s => s.Quantite - s.QuantiteReservee),
                    StockLibre = g.Where(s => s.TypeStock == TypeStock.Libre).Sum(s => s.Quantite),
                    StockReserve = g.Where(s => s.TypeStock == TypeStock.Reserve).Sum(s => s.Quantite),
                    StockImporte = g.Where(s => s.TypeStock == TypeStock.Importe).Sum(s => s.Quantite),
                    NombreEmplacements = g.Select(s => s.EmplacementPhysique).Distinct().Count(),
                    PrixMoyen = g.Average(s => s.PrixUnitaire)
                })
                .FirstOrDefaultAsync();

            if (stockInfo == null)
            {
                stockInfo = new
                {
                    ArticleId = id,
                    QuantiteTotale = 0m,
                    QuantiteReservee = 0m,
                    QuantiteDisponible = 0m,
                    StockLibre = 0m,
                    StockReserve = 0m,
                    StockImporte = 0m,
                    NombreEmplacements = 0,
                    PrixMoyen = 0m
                };
            }

            return Ok(new
            {
                Article = article,
                Stock = stockInfo,
                AlerteStock = stockInfo.QuantiteTotale <= article.SeuilAlerte,
                StockCritique = stockInfo.QuantiteTotale <= article.SeuilCritique
            });
        }

        // POST: api/Article
        [HttpPost]
        public async Task<ActionResult<Article>> PostArticle(Article article)
        {
            article.DateCreation = DateTime.Now;
            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetArticle", new { id = article.Id }, article);
        }

        // PUT: api/Article/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticle(int id, Article article)
        {
            if (id != article.Id)
            {
                return BadRequest();
            }

            _context.Entry(article).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArticleExists(id))
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

        // POST: api/Article/5/Desactiver
        [HttpPost("{id}/Desactiver")]
        public async Task<IActionResult> DesactiverArticle(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            article.EstActif = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Article désactivé avec succès" });
        }

        // POST: api/Article/5/Activer
        [HttpPost("{id}/Activer")]
        public async Task<IActionResult> ActiverArticle(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            article.EstActif = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Article activé avec succès" });
        }

        // DELETE: api/Article/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            // Vérifier s'il y a des stocks, achats ou importations liés
            var hasStock = await _context.Stocks.AnyAsync(s => s.ArticleId == id);
            var hasAchats = await _context.LignesAchat.AnyAsync(la => la.ArticleId == id);
            var hasImportations = await _context.LignesImportation.AnyAsync(li => li.ArticleId == id);
            var hasBesoins = await _context.BesoinsCommandes.AnyAsync(bc => bc.ArticleId == id);

            if (hasStock || hasAchats || hasImportations || hasBesoins)
            {
                return BadRequest("Impossible de supprimer l'article car il est utilisé dans des stocks, achats, importations ou besoins de commandes. Utilisez la désactivation à la place.");
            }

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ArticleExists(int id)
        {
            return _context.Articles.Any(e => e.Id == id);
        }
    }
}

