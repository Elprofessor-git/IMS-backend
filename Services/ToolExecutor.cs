using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Exécute les appels d'outils demandés par le modèle Groq.
    /// RÈGLE DE SÉCURITÉ : seules des opérations de lecture sont exposées ici.
    /// Aucune méthode d'écriture (Create/Update/Delete) n'est accessible.
    /// </summary>
    public class ToolExecutor
    {
        private readonly ApplicationDbContext _context;
        private readonly IArticleService      _articles;
        private readonly CommandeService      _commandes;
        private readonly ImportationService   _importations;
        private readonly ILogger<ToolExecutor> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public ToolExecutor(
            ApplicationDbContext context,
            IArticleService articles,
            CommandeService commandes,
            ImportationService importations,
            ILogger<ToolExecutor> logger)
        {
            _context      = context;
            _articles     = articles;
            _commandes    = commandes;
            _importations = importations;
            _logger       = logger;
        }

        public async Task<string> ExecuteAsync(string toolName, JsonNode args)
        {
            _logger.LogInformation("Outil : {Tool} | args : {Args}", toolName, args.ToJsonString());
            try
            {
                return toolName switch
                {
                    "get_articles"     => await GetArticles(args),
                    "get_stock"        => await GetStock(args),
                    "get_commandes"    => await GetCommandes(args),
                    "get_achats"       => await GetAchats(args),
                    "get_importations" => await GetImportations(args),
                    "get_mouvements"   => await GetMouvements(args),
                    _ => Err($"Outil inconnu : {toolName}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur exécution outil {Tool}", toolName);
                return Err(ex.Message);
            }
        }

        // ── Outils ───────────────────────────────────────────────────────────────

        private async Task<string> GetArticles(JsonNode args)
        {
            var term = args["recherche"]?.GetValue<string>();

            IEnumerable<Article> list = string.IsNullOrWhiteSpace(term)
                ? (await _articles.GetArticlesAsync(1, 30)).Data
                : await _articles.SearchArticlesAsync(term);

            var result = list.Select(a => new
            {
                a.Id,
                a.Designation,
                a.Reference,
                a.Categorie,
                a.SousCategorie,
                a.Unite,
                a.SeuilAlerte,
                a.SeuilCritique,
                a.EstActif,
                StockDisponible = a.Stocks?.Sum(s => s.Quantite - s.QuantiteReservee) ?? 0
            }).ToList();

            return Json(new { count = result.Count, articles = result });
        }

        private async Task<string> GetStock(JsonNode args)
        {
            var articleId = args["articleId"]?.GetValue<int?>();
            if (articleId is null)
                return Err("articleId est obligatoire pour get_stock.");

            var article = await _articles.GetArticleByIdAsync(articleId.Value);
            if (article is null)
                return Err($"Article id={articleId} introuvable.");

            var stockTotal = await _articles.GetStockTotalAsync(articleId.Value);

            var parType = article.Stocks?
                .GroupBy(s => s.TypeStock.ToString())
                .Select(g => new
                {
                    TypeStock        = g.Key,
                    Quantite         = g.Sum(s => s.Quantite),
                    QuantiteReservee = g.Sum(s => s.QuantiteReservee),
                    Disponible       = g.Sum(s => s.Quantite - s.QuantiteReservee)
                }).ToList();

            return Json(new
            {
                article   = new { article.Id, article.Designation, article.Reference, article.Unite },
                stockTotal,
                parType
            });
        }

        private async Task<string> GetCommandes(JsonNode args)
        {
            var statutStr = args["statut"]?.GetValue<string>();
            var marqueId  = args["marqueId"]?.GetValue<int?>();

            var all = await _commandes.GetAllCommandesAsync();
            var filtered = all.AsEnumerable();

            if (!string.IsNullOrEmpty(statutStr) &&
                Enum.TryParse<StatutCommande>(statutStr, ignoreCase: true, out var statut))
                filtered = filtered.Where(c => c.Statut == statut);

            if (marqueId.HasValue)
                filtered = filtered.Where(c => c.MarqueId == marqueId.Value);

            var result = filtered
                .OrderByDescending(c => c.DateCreation)
                .Take(25)
                .Select(c => new
                {
                    c.Id,
                    c.NumeroCommande,
                    c.TitreCommande,
                    Client = c.Client?.Nom,
                    Statut = c.Statut.ToString(),
                    c.DateLivraisonSouhaitee,
                    c.MontantTotal,
                    c.PourcentageRessourcesCouvertes
                }).ToList();

            return Json(new { count = result.Count, commandes = result });
        }

        private async Task<string> GetAchats(JsonNode args)
        {
            var statutStr     = args["statut"]?.GetValue<string>();
            var fournisseurId = args["fournisseurId"]?.GetValue<int?>();

            var query = _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.CommandeClient)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statutStr) &&
                Enum.TryParse<StatutAchat>(statutStr, ignoreCase: true, out var statut))
                query = query.Where(a => a.Statut == statut);

            if (fournisseurId.HasValue)
                query = query.Where(a => a.FournisseurId == fournisseurId.Value);

            var achats = await query
                .OrderByDescending(a => a.DateAchat)
                .Take(25)
                .Select(a => new
                {
                    a.Id,
                    a.NumeroAchat,
                    Fournisseur = a.Fournisseur.NomEntreprise,
                    Commande    = a.CommandeClient != null ? a.CommandeClient.NumeroCommande : null,
                    Statut      = a.Statut.ToString(),
                    a.MontantTotal,
                    a.Devise,
                    a.DateLivraisonPrevue
                })
                .ToListAsync();

            return Json(new { count = achats.Count, achats });
        }

        private async Task<string> GetImportations(JsonNode args)
        {
            var statutStr = args["statut"]?.GetValue<string>();

            var all = await _importations.GetAllAsync();
            var filtered = all.AsEnumerable();

            if (!string.IsNullOrEmpty(statutStr) &&
                Enum.TryParse<StatutImportation>(statutStr, ignoreCase: true, out var statut))
                filtered = filtered.Where(i => i.Statut == statut);

            var result = filtered
                .OrderByDescending(i => i.DateCreation)
                .Take(25)
                .Select(i => new
                {
                    i.Id,
                    i.ReferenceImportation,
                    Fournisseur = i.Fournisseur?.NomEntreprise,
                    Statut      = i.Statut.ToString(),
                    i.DateImportation,
                    i.MontantTotal,
                    i.Devise
                }).ToList();

            return Json(new { count = result.Count, importations = result });
        }

        private async Task<string> GetMouvements(JsonNode args)
        {
            var articleId = args["articleId"]?.GetValue<int?>();
            var debutStr  = args["dateDebut"]?.GetValue<string>();
            var finStr    = args["dateFin"]?.GetValue<string>();

            DateTime? debut = DateTimeOffset.TryParse(debutStr, out var d)
                ? d.UtcDateTime : null;
            DateTime? fin = DateTimeOffset.TryParse(finStr, out var f)
                ? f.UtcDateTime.AddDays(1) : null;   // inclut la journée de fin

            var query = _context.MouvementsStock
                .Include(m => m.Stock).ThenInclude(s => s.Article)
                .AsQueryable();

            if (articleId.HasValue)
                query = query.Where(m => m.Stock.ArticleId == articleId.Value);

            if (debut.HasValue)
                query = query.Where(m => m.DateMouvement >= debut.Value);

            if (fin.HasValue)
                query = query.Where(m => m.DateMouvement < fin.Value);

            var mouvements = await query
                .OrderByDescending(m => m.DateMouvement)
                .Take(50)
                .Select(m => new
                {
                    m.Id,
                    Article          = m.Stock.Article.Designation,
                    TypeStock        = m.Stock.TypeStock.ToString(),
                    TypeMouvement    = m.TypeMouvement.ToString(),
                    OrigineMouvement = m.OrigineMouvement.ToString(),
                    m.Quantite,
                    m.QuantiteAvant,
                    m.QuantiteApres,
                    m.Motif,
                    m.DateMouvement,
                    m.EffectuePar
                })
                .ToListAsync();

            return Json(new { count = mouvements.Count, mouvements });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string Json(object obj) =>
            JsonSerializer.Serialize(obj, _jsonOpts);

        private static string Err(string message) =>
            JsonSerializer.Serialize(new { error = message }, _jsonOpts);
    }
}
