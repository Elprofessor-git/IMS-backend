using Backend_Gestion_Magasin_API.Dtos.Article;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ArticleController : ControllerBase
    {
        private readonly IArticleService _articleService;
        private readonly ApplicationDbContext _context;

        public ArticleController(IArticleService articleService, ApplicationDbContext context)
        {
            _articleService = articleService;
            _context = context;
        }

        [HttpGet]
        [RequireModulePermission("articles", requireWrite: false)]
        public async Task<IActionResult> GetArticles([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var articles = await _articleService.GetArticlesAsync(pageNumber, pageSize);
            return Ok(articles);
        }

        [HttpGet("{id}")]
        [RequireModulePermission("articles", requireWrite: false)]
        public async Task<IActionResult> GetArticle(int id)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null)
            {
                return NotFound();
            }
            return Ok(article);
        }

        [HttpGet("Search/{terme}")]
        [RequireModulePermission("articles", requireWrite: false)]
        public async Task<IActionResult> SearchArticles(string terme)
        {
            var articles = await _articleService.SearchArticlesAsync(terme);
            return Ok(articles);
        }

        [HttpGet("ByCategorie/{categorie}")]
        [RequireModulePermission("articles", requireWrite: false)]
        public async Task<IActionResult> GetArticlesByCategorie(string categorie)
        {
            var articles = await _articleService.GetArticlesByCategorieAsync(categorie);
            return Ok(articles);
        }

        [HttpGet("{id}/StockTotal")]
        [RequireModulePermission("articles", requireWrite: false)]
        public async Task<IActionResult> GetStockTotal(int id)
        {
            var stockInfo = await _articleService.GetStockTotalAsync(id);
            if (stockInfo == null)
            {
                return NotFound();
            }
            return Ok(stockInfo);
        }

        [HttpGet("{id}/HistoriquePrix")]
        [RequireModulePermission("articles", requireWrite: false)]
        public async Task<IActionResult> GetHistoriquePrix(int id)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null)
            {
                return NotFound();
            }
            var historique = await _articleService.GetHistoriquePrixAsync(id);
            return Ok(historique);
        }

        [HttpPut("{id}/PrixUnitaire")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> PutPrixUnitaire(int id, UpdatePrixArticleDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var changed = await _articleService.UpdatePrixArticleAsync(id, dto);
                return changed
                    ? Ok(new { message = "Prix de référence mis à jour" })
                    : Ok(new { message = "Prix inchangé — aucune entrée d'historique créée" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> PostArticle(CreateArticleDto articleDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var newArticle = await _articleService.CreateArticleAsync(articleDto);
            return CreatedAtAction(nameof(GetArticle), new { id = newArticle.Id }, newArticle);
        }

        [HttpPut("{id}")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> PutArticle(int id, UpdateArticleDto articleDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                await _articleService.UpdateArticleAsync(id, articleDto);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            try
            {
                await _articleService.DeleteArticleAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/Desactiver")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> DesactiverArticle(int id)
        {
            var success = await _articleService.DeactivateArticleAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return Ok(new { message = "Article désactivé avec succès" });
        }

        [HttpPost("{id}/Activer")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> ActiverArticle(int id)
        {
            var success = await _articleService.ActivateArticleAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return Ok(new { message = "Article activé avec succès" });
        }

        [HttpPost("{id}/image")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> UploadImage(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            try
            {
                var imageUrl = await _articleService.UploadImageAsync(id, file);
                return Ok(new { imageUrl });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── Catalogue Article ↔ Fournisseur (multi-sourcing, §5.2) ────────────
        // Un seul Article canonique, plusieurs fournisseurs enregistrés dessus
        // (référence fournisseur + prix habituel + délai d'approvisionnement).
        // Le blocage existant de SoumettreAchat continue de fonctionner sans changement.

        [HttpGet("{id}/Fournisseurs")]
        [RequireModulePermission("articles", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<ArticleFournisseurDto>>> GetArticleFournisseurs(int id)
        {
            if (!await ArticleExiste(id))
                return NotFound();

            return await _context.ArticleFournisseurs
                .AsNoTracking()
                .Where(af => af.ArticleId == id)
                .Select(af => new ArticleFournisseurDto
                {
                    Id = af.Id,
                    ArticleId = af.ArticleId,
                    FournisseurId = af.FournisseurId,
                    ReferenceFournisseur = af.ReferenceFournisseur,
                    PrixHabituel = af.PrixHabituel,
                    DelaiApprovisionnementJours = af.DelaiApprovisionnementJours,
                    EstActif = af.EstActif,
                    Fournisseur = af.Fournisseur != null ? new FournisseurInfoDto
                    {
                        Id = af.Fournisseur.Id,
                        NomEntreprise = af.Fournisseur.NomEntreprise
                    } : null
                })
                .OrderBy(af => af.FournisseurId)
                .ToListAsync();
        }

        [HttpPost("{id}/Fournisseurs")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<ActionResult<ArticleFournisseurDto>> CreateArticleFournisseur(int id, CreateArticleFournisseurDto dto)
        {
            if (!await ArticleExiste(id))
                return NotFound();

            var fournisseurExiste = await _context.Fournisseurs.AnyAsync(f => f.Id == dto.FournisseurId);
            if (!fournisseurExiste)
                return BadRequest(new { message = "Fournisseur introuvable." });

            var doublon = await _context.ArticleFournisseurs
                .AnyAsync(af => af.ArticleId == id && af.FournisseurId == dto.FournisseurId);
            if (doublon)
                return Conflict(new { message = "Ce fournisseur est déjà enregistré pour cet article." });

            var lien = new ArticleFournisseur
            {
                ArticleId = id,
                FournisseurId = dto.FournisseurId,
                ReferenceFournisseur = dto.ReferenceFournisseur,
                PrixHabituel = dto.PrixHabituel,
                DelaiApprovisionnementJours = dto.DelaiApprovisionnementJours,
                EstActif = dto.EstActif
            };

            _context.ArticleFournisseurs.Add(lien);
            await _context.SaveChangesAsync();

            var dtoCree = await _context.ArticleFournisseurs
                .Where(af => af.Id == lien.Id)
                .Select(af => new ArticleFournisseurDto
                {
                    Id = af.Id,
                    ArticleId = af.ArticleId,
                    FournisseurId = af.FournisseurId,
                    ReferenceFournisseur = af.ReferenceFournisseur,
                    PrixHabituel = af.PrixHabituel,
                    DelaiApprovisionnementJours = af.DelaiApprovisionnementJours,
                    EstActif = af.EstActif,
                    Fournisseur = af.Fournisseur != null ? new FournisseurInfoDto
                    {
                        Id = af.Fournisseur.Id,
                        NomEntreprise = af.Fournisseur.NomEntreprise
                    } : null
                })
                .FirstAsync();

            return CreatedAtAction(nameof(GetArticleFournisseurs), new { id }, dtoCree);
        }

        [HttpPut("{id}/Fournisseurs/{fournisseurId}")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> UpdateArticleFournisseur(int id, int fournisseurId, UpdateArticleFournisseurDto dto)
        {
            var lien = await _context.ArticleFournisseurs
                .FirstOrDefaultAsync(af => af.ArticleId == id && af.FournisseurId == fournisseurId);
            if (lien == null)
                return NotFound();

            lien.ReferenceFournisseur = dto.ReferenceFournisseur;
            lien.PrixHabituel = dto.PrixHabituel;
            lien.DelaiApprovisionnementJours = dto.DelaiApprovisionnementJours;
            lien.EstActif = dto.EstActif;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}/Fournisseurs/{fournisseurId}")]
        [RequireModulePermission("articles", requireWrite: true)]
        public async Task<IActionResult> DeleteArticleFournisseur(int id, int fournisseurId)
        {
            var lien = await _context.ArticleFournisseurs
                .FirstOrDefaultAsync(af => af.ArticleId == id && af.FournisseurId == fournisseurId);
            if (lien == null)
                return NotFound();

            _context.ArticleFournisseurs.Remove(lien);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<bool> ArticleExiste(int id)
        {
            return await _context.Articles.AnyAsync(a => a.Id == id);
        }
    }
}
