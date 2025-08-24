using Backend_Gestion_Magasin_API.Dtos.Article;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Authorization;
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

        public ArticleController(IArticleService articleService)
        {
            _articleService = articleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetArticles([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var articles = await _articleService.GetArticlesAsync(pageNumber, pageSize);
            return Ok(articles);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetArticle(int id)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null)
            {
                return NotFound();
            }
            return Ok(article);
        }

        [HttpPost]
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

        [HttpGet("Search/{terme}")]
        public async Task<IActionResult> SearchArticles(string terme)
        {
            var articles = await _articleService.SearchArticlesAsync(terme);
            return Ok(articles);
        }

        [HttpGet("ByCategorie/{categorie}")]
        public async Task<IActionResult> GetArticlesByCategorie(string categorie)
        {
            var articles = await _articleService.GetArticlesByCategorieAsync(categorie);
            return Ok(articles);
        }

        [HttpGet("{id}/StockTotal")]
        public async Task<IActionResult> GetStockTotal(int id)
        {
            var stockInfo = await _articleService.GetStockTotalAsync(id);
            if (stockInfo == null)
            {
                return NotFound();
            }
            return Ok(stockInfo);
        }

        [HttpPost("{id}/Desactiver")]
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
    }
}
