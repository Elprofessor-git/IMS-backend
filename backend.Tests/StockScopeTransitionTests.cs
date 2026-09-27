using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    public class StockScopeTransitionTests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;
        private TestUser? _admin;

        public StockScopeTransitionTests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _admin = await _factory.CreateUserAsync(
                "stock-test-admin",
                "Admin",
                "Stock",
                r =>
                {
                    r.EstAdministrateur = true;
                    r.PeutGererStock = true;
                    r.PeutValiderStock = true;
                });
        }

        public async Task DisposeAsync() { }

        private static async Task<HttpResponseMessage> PostStockAsync(HttpClient client, object dto)
        {
            return await client.PostAsJsonAsync("/api/Stock", dto);
        }

        private static async Task<HttpResponseMessage> PutStockAsync(HttpClient client, int id, object dto)
        {
            return await client.PutAsJsonAsync($"/api/Stock/{id}", dto);
        }

        private static async Task<HttpResponseMessage> GetStockAsync(HttpClient client, int id)
        {
            return await client.GetAsync($"/api/Stock/{id}");
        }

        private async Task<int> CreateArticleAsync()
        {
            var res = await _admin!.Client.PostAsJsonAsync("/api/Article", new
            {
                designation = "Test Article " + Guid.NewGuid(),
                unite = "m",
                laize = 1.6,
                categorie = "Tissu"
            });
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json).GetProperty("id").GetInt32();
        }

        [Fact]
        public async Task PostStock_ScopeIdentique_Reussi()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock avec CommandeClientId
            var res = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100,
                CommandeClientId = 1
            });

            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        [Fact]
        public async Task PostStock_MultiplesScopes_Rejete()
        {
            var articleId = await CreateArticleAsync();

            // Tenter de créer avec CommandeClientId ET GroupeCommandeId
            var res = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100,
                CommandeClientId = 1,
                GroupeCommandeId = 1
            });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var text = await res.Content.ReadAsStringAsync();
            Assert.Contains("ne peut appartenir qu'à un seul scope", text);
        }

        [Fact]
        public async Task PutStock_ScopeIdentique_Reussi()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock initial avec CommandeClientId
            var createRes = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100,
                CommandeClientId = 1
            });
            createRes.EnsureSuccessStatusCode();
            var createJson = await createRes.Content.ReadAsStringAsync();
            var stockId = JsonSerializer.Deserialize<JsonElement>(createJson).GetProperty("id").GetInt32();

            // PUT avec le même scope (CommandeClientId = 1)
            var putRes = await PutStockAsync(_admin!.Client, stockId, new
            {
                ArticleId = articleId,
                Quantite = 150,
                CommandeClientId = 1
            });

            Assert.Equal(HttpStatusCode.NoContent, putRes.StatusCode);
        }

        [Fact]
        public async Task PutStock_TransitionGroupeVersCommande_Rejete()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock initial avec GroupeCommandeId
            var createRes = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100,
                GroupeCommandeId = 1
            });
            createRes.EnsureSuccessStatusCode();
            var createJson = await createRes.Content.ReadAsStringAsync();
            var stockId = JsonSerializer.Deserialize<JsonElement>(createJson).GetProperty("id").GetInt32();

            // Tenter de PUT vers CommandeClientId (transition interdite)
            var putRes = await PutStockAsync(_admin!.Client, stockId, new
            {
                ArticleId = articleId,
                Quantite = 100,
                CommandeClientId = 1
            });

            Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
            var text = await putRes.Content.ReadAsStringAsync();
            Assert.Contains("Transition de scope interdite", text);
            Assert.Contains("GroupeCommande", text);
            Assert.Contains("CommandeClient", text);
        }

        [Fact]
        public async Task PutStock_TransitionCommandeVersGroupe_Rejete()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock initial avec CommandeClientId
            var createRes = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100,
                CommandeClientId = 1
            });
            createRes.EnsureSuccessStatusCode();
            var createJson = await createRes.Content.ReadAsStringAsync();
            var stockId = JsonSerializer.Deserialize<JsonElement>(createJson).GetProperty("id").GetInt32();

            // Tenter de PUT vers GroupeCommandeId (transition interdite)
            var putRes = await PutStockAsync(_admin!.Client, stockId, new
            {
                ArticleId = articleId,
                Quantite = 100,
                GroupeCommandeId = 1
            });

            Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
            var text = await putRes.Content.ReadAsStringAsync();
            Assert.Contains("Transition de scope interdite", text);
            Assert.Contains("CommandeClient", text);
            Assert.Contains("GroupeCommande", text);
        }

        [Fact]
        public async Task PutStock_TransitionClientVersPlateforme_Rejete()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock initial avec ClientId
            var createRes = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100,
                ClientId = 1
            });
            createRes.EnsureSuccessStatusCode();
            var createJson = await createRes.Content.ReadAsStringAsync();
            var stockId = JsonSerializer.Deserialize<JsonElement>(createJson).GetProperty("id").GetInt32();

            // Tenter de PUT vers PlateformeId
            var putRes = await PutStockAsync(_admin!.Client, stockId, new
            {
                ArticleId = articleId,
                Quantite = 100,
                PlateformeId = 1
            });

            Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
            var text = await putRes.Content.ReadAsStringAsync();
            Assert.Contains("Transition de scope interdite", text);
        }

        [Fact]
        public async Task PutStock_ScopeVersNull_Autorise()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock initial avec CommandeClientId
            var createRes = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100,
                CommandeClientId = 1
            });
            createRes.EnsureSuccessStatusCode();
            var createJson = await createRes.Content.ReadAsStringAsync();
            var stockId = JsonSerializer.Deserialize<JsonElement>(createJson).GetProperty("id").GetInt32();

            // PUT en retirant le scope (devient Libre)
            var putRes = await PutStockAsync(_admin!.Client, stockId, new
            {
                ArticleId = articleId,
                Quantite = 100
            });

            Assert.Equal(HttpStatusCode.NoContent, putRes.StatusCode);

            // Vérifier que le scope est bien null
            var getRes = await GetStockAsync(_admin!.Client, stockId);
            getRes.EnsureSuccessStatusCode();
            var getJson = await getRes.Content.ReadAsStringAsync();
            var stock = JsonSerializer.Deserialize<JsonElement>(getJson);
            Assert.False(stock.GetProperty("commandeClientId").ValueKind != JsonValueKind.Null, "CommandeClientId should be null");
            Assert.Equal("Libre", stock.GetProperty("typeStock").GetString());
        }

        [Fact]
        public async Task PutStock_NullVersScope_Rejete()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock initial SANS scope (Libre)
            var createRes = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100
            });
            createRes.EnsureSuccessStatusCode();
            var createJson = await createRes.Content.ReadAsStringAsync();
            var stockId = JsonSerializer.Deserialize<JsonElement>(createJson).GetProperty("id").GetInt32();

            // Tenter de PUT vers CommandeClientId (transition depuis Libre interdite)
            var putRes = await PutStockAsync(_admin!.Client, stockId, new
            {
                ArticleId = articleId,
                Quantite = 100,
                CommandeClientId = 1
            });

            Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
            var text = await putRes.Content.ReadAsStringAsync();
            Assert.Contains("Transition de scope interdite", text);
        }

        [Fact]
        public async Task PutStock_AucunScope_Reussi()
        {
            var articleId = await CreateArticleAsync();

            // Créer stock initial SANS scope
            var createRes = await PostStockAsync(_admin!.Client, new
            {
                ArticleId = articleId,
                Quantite = 100
            });
            createRes.EnsureSuccessStatusCode();
            var createJson = await createRes.Content.ReadAsStringAsync();
            var stockId = JsonSerializer.Deserialize<JsonElement>(createJson).GetProperty("id").GetInt32();

            // PUT sans scope
            var putRes = await PutStockAsync(_admin!.Client, stockId, new
            {
                ArticleId = articleId,
                Quantite = 150
            });

            Assert.Equal(HttpStatusCode.NoContent, putRes.StatusCode);
        }
    }
}