using Backend_Gestion_Magasin_API.Dtos;
using Backend_Gestion_Magasin_API.Dtos.Article;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.Http;

namespace Backend_Gestion_Magasin_API.Services
{
    public interface IArticleService
    {
        Task<PaginatedResponse<Article>> GetArticlesAsync(int pageNumber, int pageSize);
        Task<Article?> GetArticleByIdAsync(int id);
        Task<IEnumerable<Article>> SearchArticlesAsync(string term);
        Task<IEnumerable<Article>> GetArticlesByCategorieAsync(string categorie);
        Task<object?> GetStockTotalAsync(int id);
        Task<Article> CreateArticleAsync(CreateArticleDto articleDto);
        Task UpdateArticleAsync(int id, UpdateArticleDto articleDto);
        Task<bool> DeactivateArticleAsync(int id);
        Task<bool> ActivateArticleAsync(int id);
        Task DeleteArticleAsync(int id);
        Task<string> UploadImageAsync(int id, IFormFile file);
    }
}
