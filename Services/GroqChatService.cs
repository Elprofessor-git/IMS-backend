using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend_Gestion_Magasin_API.Services
{
    public class GroqChatService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GroqChatService> _logger;
        private readonly string _apiKey;

        private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
        private const string Model = "llama-3.3-70b-versatile";
        private const int MaxTurns = 50;

        private const string SystemPrompt = """
            Tu es un assistant IA pour un système IMS (Inventory Management System)
            d'un atelier textile tunisien.
            Tu réponds TOUJOURS en français.
            Tu utilises les outils disponibles pour accéder aux données réelles du système.
            Tu ne modifies JAMAIS les données — tu es en lecture seule.
            Tu es concis et précis. Quand tu listes des données, utilise des listes claires.
            Si une donnée n'existe pas, dis-le clairement plutôt qu'inventer.
            """;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public GroqChatService(HttpClient httpClient, ApplicationDbContext context,
            IConfiguration configuration, ILogger<GroqChatService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _logger = logger;
            _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")
                ?? configuration["GroqSettings:ApiKey"]
                ?? throw new InvalidOperationException("GROQ_API_KEY non configuré.");
        }

        public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
        {
            try
            {
                var messages = new List<object>
                {
                    new { role = "system", content = SystemPrompt }
                };

                if (request.History != null)
                {
                    foreach (var msg in request.History)
                        messages.Add(new { role = msg.Role, content = msg.Content });
                }

                messages.Add(new { role = "user", content = request.Message });

                for (int turn = 0; turn < MaxTurns; turn++)
                {
                    var groqRequest = new
                    {
                        model = Model,
                        messages,
                        tools = ChatTools.All,
                        tool_choice = "auto",
                        max_tokens = 1024,
                        temperature = 0.3
                    };

                    var json = JsonSerializer.Serialize(groqRequest, _jsonOptions);
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, GroqApiUrl);
                    httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
                    httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    var httpResponse = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await httpResponse.Content.ReadAsStringAsync();

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Groq API error {Status}: {Body}", httpResponse.StatusCode, responseBody);
                        return ErrorResponse("Erreur lors de la communication avec le service IA.", request.SessionId);
                    }

                    var groqResponse = JsonNode.Parse(responseBody);
                    var choice = groqResponse?["choices"]?[0];
                    var message = choice?["message"];
                    var finishReason = choice?["finish_reason"]?.GetValue<string>();

                    if (finishReason == "stop" || message?["tool_calls"] == null)
                    {
                        var content = message?["content"]?.GetValue<string>() ?? "";
                        return new ChatResponse
                        {
                            Response = content,
                            SessionId = request.SessionId,
                            Success = true,
                            Timestamp = DateTime.UtcNow
                        };
                    }

                    var toolCalls = message["tool_calls"]!.AsArray();

                    messages.Add(new
                    {
                        role = "assistant",
                        content = message["content"]?.GetValue<string>() ?? "",
                        tool_calls = toolCalls.Select(tc => new
                        {
                            id = tc!["id"]!.GetValue<string>(),
                            type = "function",
                            function = new
                            {
                                name = tc["function"]!["name"]!.GetValue<string>(),
                                arguments = tc["function"]!["arguments"]!.GetValue<string>()
                            }
                        }).ToArray()
                    });

                    foreach (var toolCall in toolCalls)
                    {
                        var toolId = toolCall!["id"]!.GetValue<string>();
                        var toolName = toolCall["function"]!["name"]!.GetValue<string>();
                        var toolArgsRaw = toolCall["function"]!["arguments"]!.GetValue<string>();

                        _logger.LogInformation("Exécution outil: {Tool} avec args: {Args}", toolName, toolArgsRaw);

                        var toolArgs = JsonNode.Parse(toolArgsRaw) ?? new JsonObject();
                        var toolResult = await ExecuteToolAsync(toolName, toolArgs);

                        messages.Add(new
                        {
                            role = "tool",
                            tool_call_id = toolId,
                            content = toolResult
                        });
                    }
                }

                _logger.LogWarning("Agent loop a atteint la limite de {Max} tours.", MaxTurns);
                return ErrorResponse("Le traitement a pris trop de cycles. Reformulez votre question.", request.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans GroqChatService");
                return ErrorResponse("Une erreur inattendue s'est produite.", request.SessionId);
            }
        }

        public async Task<bool> IsApiAvailableAsync()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://api.groq.com/openai/v1/models");
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");
                var resp = await _httpClient.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> ExecuteToolAsync(string toolName, JsonNode args)
        {
            try
            {
                return toolName switch
                {
                    "get_articles"       => await GetArticles(args),
                    "get_article_stock"  => await GetArticleStock(args),
                    "get_stock_alertes"  => await GetStockAlertes(),
                    "get_commandes"      => await GetCommandes(args),
                    "get_commande_detail"=> await GetCommandeDetail(args),
                    "get_importations"   => await GetImportations(args),
                    "get_achats"         => await GetAchats(args),
                    _ => JsonSerializer.Serialize(new { error = $"Outil inconnu: {toolName}" })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur exécution outil {Tool}", toolName);
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private async Task<string> GetArticles(JsonNode args)
        {
            var categorie  = args["categorie"]?.GetValue<string>();
            var searchTerm = args["searchTerm"]?.GetValue<string>();

            var query = _context.Articles.AsQueryable();

            if (!string.IsNullOrEmpty(categorie))
                query = query.Where(a => a.Categorie == categorie);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(a =>
                    a.Designation.Contains(searchTerm) ||
                    a.Reference.Contains(searchTerm));

            var articles = await query
                .OrderBy(a => a.Designation)
                .Take(20)
                .Select(a => new
                {
                    a.Id,
                    a.Designation,
                    a.Reference,
                    a.Categorie,
                    a.SousCategorie,
                    a.Unite,
                    a.PrixUnitaireMoyen,
                    a.SeuilAlerte,
                    a.EstActif
                })
                .ToListAsync();

            return JsonSerializer.Serialize(new { count = articles.Count, articles }, _jsonOptions);
        }

        private async Task<string> GetArticleStock(JsonNode args)
        {
            var articleId = args["articleId"]?.GetValue<int>() ?? 0;

            var article = await _context.Articles
                .Where(a => a.Id == articleId)
                .Select(a => new { a.Id, a.Designation, a.Reference, a.Unite })
                .FirstOrDefaultAsync();

            if (article == null)
                return JsonSerializer.Serialize(new { error = "Article non trouvé" });

            var stocks = await _context.Stocks
                .Where(s => s.ArticleId == articleId)
                .GroupBy(s => s.TypeStock)
                .Select(g => new
                {
                    TypeStock        = g.Key.ToString(),
                    QuantiteTotale   = g.Sum(s => s.Quantite),
                    QuantiteReservee = g.Sum(s => s.QuantiteReservee)
                })
                .ToListAsync();

            var totalQuantite = stocks.Sum(s => s.QuantiteTotale);

            return JsonSerializer.Serialize(new { article, totalQuantite, stocks }, _jsonOptions);
        }

        private async Task<string> GetStockAlertes()
        {
            var alertes = await _context.Stocks
                .Include(s => s.Article)
                .Where(s => s.Quantite <= s.Article.SeuilAlerte)
                .OrderBy(s => s.Quantite)
                .Take(20)
                .Select(s => new
                {
                    s.Id,
                    Article      = s.Article.Designation,
                    Reference    = s.Article.Reference,
                    s.Quantite,
                    SeuilAlerte  = s.Article.SeuilAlerte,
                    SeuilCritique= s.Article.SeuilCritique,
                    EstCritique  = s.Quantite <= s.Article.SeuilCritique,
                    TypeStock    = s.TypeStock.ToString()
                })
                .ToListAsync();

            return JsonSerializer.Serialize(new { count = alertes.Count, alertes }, _jsonOptions);
        }

        private async Task<string> GetCommandes(JsonNode args)
        {
            var statutStr = args["statut"]?.GetValue<string>();

            var query = _context.CommandesClients
                .Include(c => c.Client)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statutStr) && Enum.TryParse<StatutCommande>(statutStr, out var statut))
                query = query.Where(c => c.Statut == statut);

            var commandes = await query
                .OrderByDescending(c => c.DateCreation)
                .Take(20)
                .Select(c => new
                {
                    c.Id,
                    c.NumeroCommande,
                    c.TitreCommande,
                    Client  = c.Client.Nom + (c.Client.NomEntreprise != null ? $" ({c.Client.NomEntreprise})" : ""),
                    Statut  = c.Statut.ToString(),
                    c.DateLivraisonSouhaitee,
                    c.MontantTotal,
                    c.PourcentageRessourcesCouvertes
                })
                .ToListAsync();

            return JsonSerializer.Serialize(new { count = commandes.Count, commandes }, _jsonOptions);
        }

        private async Task<string> GetCommandeDetail(JsonNode args)
        {
            var commandeId = args["commandeId"]?.GetValue<int>() ?? 0;

            var commande = await _context.CommandesClients
                .Include(c => c.Client)
                .Include(c => c.Besoins).ThenInclude(b => b.Article)
                .Include(c => c.Taches)
                .Include(c => c.Achats).ThenInclude(a => a.Fournisseur)
                .FirstOrDefaultAsync(c => c.Id == commandeId);

            if (commande == null)
                return JsonSerializer.Serialize(new { error = "Commande non trouvée" });

            var result = new
            {
                commande.Id,
                commande.NumeroCommande,
                commande.TitreCommande,
                Statut  = commande.Statut.ToString(),
                Client  = commande.Client?.Nom,
                commande.DateLivraisonSouhaitee,
                commande.MontantTotal,
                commande.PourcentageRessourcesCouvertes,
                Besoins = commande.Besoins.Select(b => new
                {
                    Article              = b.Article?.Designation,
                    b.QuantiteTotale,
                    b.QuantiteCouverte,
                    b.EstCompletementCouvert
                }),
                Taches = commande.Taches.Select(t => new
                {
                    t.Titre,
                    Statut = t.Statut.ToString()
                }),
                Achats = commande.Achats.Select(a => new
                {
                    a.NumeroAchat,
                    Fournisseur = a.Fournisseur?.NomEntreprise,
                    Statut      = a.Statut.ToString(),
                    a.MontantTotal
                })
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }

        private async Task<string> GetImportations(JsonNode args)
        {
            var statutStr    = args["statut"]?.GetValue<string>();
            var fournisseurId = args["fournisseurId"]?.GetValue<int?>();

            var query = _context.Importations
                .Include(i => i.Fournisseur)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statutStr) && Enum.TryParse<StatutImportation>(statutStr, out var statut))
                query = query.Where(i => i.Statut == statut);

            if (fournisseurId.HasValue)
                query = query.Where(i => i.FournisseurId == fournisseurId.Value);

            var importations = await query
                .OrderByDescending(i => i.DateCreation)
                .Take(20)
                .Select(i => new
                {
                    i.Id,
                    i.ReferenceImportation,
                    Fournisseur = i.Fournisseur.NomEntreprise,
                    Statut      = i.Statut.ToString(),
                    i.DateImportation,
                    i.MontantTotal,
                    i.Devise
                })
                .ToListAsync();

            return JsonSerializer.Serialize(new { count = importations.Count, importations }, _jsonOptions);
        }

        private async Task<string> GetAchats(JsonNode args)
        {
            var statutStr = args["statut"]?.GetValue<string>();
            var commandeId = args["commandeId"]?.GetValue<int?>();

            var query = _context.Achats
                .Include(a => a.Fournisseur)
                .Include(a => a.CommandeClient)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statutStr) && Enum.TryParse<StatutAchat>(statutStr, out var statut))
                query = query.Where(a => a.Statut == statut);

            if (commandeId.HasValue)
                query = query.Where(a => a.CommandeClientId == commandeId.Value);

            var achats = await query
                .OrderByDescending(a => a.DateCreation)
                .Take(20)
                .Select(a => new
                {
                    a.Id,
                    a.NumeroAchat,
                    Fournisseur = a.Fournisseur.NomEntreprise,
                    Commande    = a.CommandeClient != null ? a.CommandeClient.NumeroCommande : null,
                    Statut      = a.Statut.ToString(),
                    a.MontantTotal,
                    a.DateLivraisonPrevue
                })
                .ToListAsync();

            return JsonSerializer.Serialize(new { count = achats.Count, achats }, _jsonOptions);
        }

        private static ChatResponse ErrorResponse(string message, string? sessionId) => new()
        {
            Response  = message,
            SessionId = sessionId,
            Success   = false,
            Error     = message,
            Timestamp = DateTime.UtcNow
        };
    }
}
