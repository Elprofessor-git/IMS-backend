using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos;
using Backend_Gestion_Magasin_API.Dtos.Article;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend_Gestion_Magasin_API.Services
{
    public class ArticleService : IArticleService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ArticleService(ApplicationDbContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        public async Task<Article> CreateArticleAsync(CreateArticleDto articleDto)
        {
            var article = new Article
            {
                Designation = articleDto.Designation,
                Description = articleDto.Description,
                Categorie = articleDto.Categorie,
                SousCategorie = articleDto.SousCategorie,
                Unite = articleDto.Unite,
                Marque = articleDto.Marque,
                Reference = articleDto.Reference,
                Caracteristiques = articleDto.Caracteristiques,
                SeuilAlerte = articleDto.SeuilAlerte,
                SeuilCritique = articleDto.SeuilCritique,
                DateCreation = System.DateTime.UtcNow,
                EstActif = true
            };

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();
            return article;
        }

        public async Task DeleteArticleAsync(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                throw new KeyNotFoundException("Article not found.");
            }

            var hasStock = await _context.Stocks.AnyAsync(s => s.ArticleId == id);
            var hasAchats = await _context.LignesAchat.AnyAsync(la => la.ArticleId == id);
            var hasImportations = await _context.LignesImportation.AnyAsync(li => li.ArticleId == id);
            var hasBesoins = await _context.BesoinsCommandes.AnyAsync(bc => bc.ArticleId == id);

            if (hasStock || hasAchats || hasImportations || hasBesoins)
            {
                throw new InvalidOperationException("Article cannot be deleted as it is in use.");
            }

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
        }

        public async Task<PaginatedResponse<Article>> GetArticlesAsync(int pageNumber, int pageSize)
        {
            var query = _context.Articles
                .Where(a => a.EstActif)
                .Include(a => a.Stocks);

            var totalCount = await query.CountAsync();

            var articles = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResponse<Article>(articles, pageNumber, pageSize, totalCount);
        }

        public async Task<Article?> GetArticleByIdAsync(int id)
        {
            return await _context.Articles
                .Include(a => a.Stocks)
                .Include(a => a.BesoinsCommande)
                .Include(a => a.LignesAchat)
                .Include(a => a.LignesImportation)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<IEnumerable<Article>> GetArticlesByCategorieAsync(string categorie)
        {
            return await _context.Articles
                .Where(a => a.EstActif && a.Categorie == categorie)
                .Include(a => a.Stocks)
                .ToListAsync();
        }

        public async Task<object?> GetStockTotalAsync(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return null;

            return await _context.Stocks
                .Where(s => s.ArticleId == id)
                .GroupBy(s => s.ArticleId)
                .Select(g => new
                {
                    ArticleId = g.Key,
                    QuantiteTotale = g.Sum(s => s.Quantite),
                    QuantiteReservee = g.Sum(s => s.QuantiteReservee),
                    QuantiteDisponible = g.Sum(s => s.Quantite - s.QuantiteReservee)
                })
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Article>> SearchArticlesAsync(string term)
        {
            return await _context.Articles
                .Where(a => a.EstActif &&
                           (a.Designation.Contains(term) ||
                            a.Description.Contains(term) ||
                            a.Reference.Contains(term) ||
                            a.Categorie.Contains(term)))
                .Include(a => a.Stocks)
                .ToListAsync();
        }

        public async Task UpdateArticleAsync(int id, UpdateArticleDto articleDto)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                throw new KeyNotFoundException("Article not found.");
            }

            article.Designation = articleDto.Designation;
            article.Description = articleDto.Description;
            article.Categorie = articleDto.Categorie;
            article.SousCategorie = articleDto.SousCategorie;
            article.Unite = articleDto.Unite;
            article.Marque = articleDto.Marque;
            article.Reference = articleDto.Reference;
            article.Caracteristiques = articleDto.Caracteristiques;
            article.SeuilAlerte = articleDto.SeuilAlerte;
            article.SeuilCritique = articleDto.SeuilCritique;

            await _context.SaveChangesAsync();
        }

        public async Task<bool> ActivateArticleAsync(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return false;

            article.EstActif = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateArticleAsync(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return false;

            article.EstActif = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string> UploadImageAsync(int id, IFormFile file)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                throw new KeyNotFoundException("Article not found.");
            }

            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file uploaded.", nameof(file));
            }

            // TODO: For production, use a cloud storage provider like Azure Blob Storage or AWS S3.
            // Storing files on the local web server filesystem is not scalable or reliable.
            var uploadsFolderPath = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", "images");
            if (!Directory.Exists(uploadsFolderPath))
            {
                Directory.CreateDirectory(uploadsFolderPath);
            }

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"/uploads/images/{fileName}";
            article.ImageUrl = imageUrl;
            await _context.SaveChangesAsync();

            return imageUrl;
        }
    }
}
