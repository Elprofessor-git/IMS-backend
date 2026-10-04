using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    /// <summary>
    /// Lot Stock : liste paginée et filtrée, et partage de liens portant sur une
    /// sélection d'articles.
    /// </summary>
    /// <remarks>
    /// Deux propriétés sont vérifiées ici. La première : <c>GET /api/Stock/Liste</c>
    /// filtre et pagine CÔTÉ SERVEUR, et sa projection ne laisse fuir ni prix ni
    /// note. La seconde : les filtres de partage sont une liste blanche stricte —
    /// un article hors sélection, ou dont le filtre est invalide, doit produire un
    /// refus ou un ensemble vide, jamais une liste plus large que prévu.
    /// </remarks>
    public class StockListeEtPartageTests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;
        private TestUser _admin = null!;
        private TestUser _partageur = null!;

        public StockListeEtPartageTests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _admin = await _factory.CreateUserAsync(
                "stock-liste-admin", "Admin", "Stock", r => r.EstAdministrateur = true);

            // PeutPartagerLiens SANS le droit stock : sert à vérifier que la règle A
            // refuse bien un lien portant la section Stock.
            _partageur = await _factory.CreateUserAsync(
                "stock-liste-porteur", "Porteur", "Stock", r => r.PeutPartagerLiens = true);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ── Helpers de données ────────────────────────────────────────────

        private Task<int> CreerPlateformeAsync(string suf) =>
            _factory.WithDbAsync(async ctx =>
            {
                var p = new Plateforme { Nom = "PL-" + suf, EstActif = true };
                ctx.Plateformes.Add(p);
                await ctx.SaveChangesAsync();
                return p.Id;
            });

        private Task<int> CreerArticleAsync(string suf, string categorie, bool actif = true) =>
            _factory.WithDbAsync(async ctx =>
            {
                var a = new Article
                {
                    Designation = "Art " + suf,
                    Reference = "REF-" + suf,
                    Categorie = categorie,
                    EstActif = actif,
                };
                ctx.Articles.Add(a);
                await ctx.SaveChangesAsync();
                return a.Id;
            });

        private Task<int> CreerStockAsync(
            int articleId, int plateformeId, decimal quantite, TypeStock type = TypeStock.Libre) =>
            _factory.WithDbAsync(async ctx =>
            {
                var s = new Stock
                {
                    ArticleId = articleId,
                    PlateformeId = plateformeId,
                    Quantite = quantite,
                    QuantiteReservee = 1,
                    TypeStock = type,
                    PrixUnitaire = 42.5m,
                    Notes = "note interne",
                };
                ctx.Stocks.Add(s);
                await ctx.SaveChangesAsync();
                return s.Id;
            });

        private static async Task<JsonElement> LireJsonAsync(HttpResponseMessage reponse)
        {
            var corps = await reponse.Content.ReadAsStringAsync();
            if (!reponse.IsSuccessStatusCode)
            {
                // Le corps dans le message : un 400 sans sa raison est inexploitable.
                throw new HttpRequestException(
                    $"HTTP {(int)reponse.StatusCode} : {corps}", null, reponse.StatusCode);
            }
            return JsonDocument.Parse(corps).RootElement;
        }

        private Task<HttpResponseMessage> ListeAsync(
            TestUser user, string query) => user.Client.GetAsync("/api/Stock/Liste" + query);

        private static Task<HttpResponseMessage> CreerLienAsync(HttpClient client, object dto) =>
            client.PostAsJsonAsync("/api/Partage", dto);

        private async Task<JsonElement> OuvrirAsync(string token)
        {
            var requete = new HttpRequestMessage(HttpMethod.Post, "/api/Partage/Public")
            {
                Content = JsonContent.Create(new { token }),
            };
            requete.Headers.Add("X-Forwarded-For", "10.1.1.1");
            var reponse = await _factory.CreateClient().SendAsync(requete);
            reponse.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await reponse.Content.ReadAsStringAsync()).RootElement;
        }

        // ── GET /api/Stock/Liste ──────────────────────────────────────────

        [Fact]
        public async Task Liste_PropageSansPrixNiNote()
        {
            // La base est partagée par la classe de tests : on filtre sur une
            // plateforme créée ici pour n'observer que nos propres lignes.
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var article = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");
            await CreerStockAsync(article, plateforme, 100);

            var json = await LireJsonAsync(await ListeAsync(_admin, $"?plateformeId={plateforme}"));
            var item = json.GetProperty("items").EnumerateArray().Single();

            foreach (var interdit in new[] { "prixUnitaire", "prixUnitaireTND", "devise", "notes", "validePar" })
            {
                Assert.False(item.TryGetProperty(interdit, out _), $"« {interdit} » ne doit pas sortir.");
            }

            Assert.True(item.TryGetProperty("quantiteDisponible", out _));
            Assert.True(item.TryGetProperty("enAlerte", out _));
        }

        [Fact]
        public async Task Liste_FiltreParPlateformeEtParCategorie()
        {
            var p1 = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var p2 = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var articleTissus = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");
            var articleAccessoires = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Accessoires");

            var attendu = await CreerStockAsync(articleTissus, p1, 10);
            await CreerStockAsync(articleTissus, p2, 10);
            await CreerStockAsync(articleAccessoires, p1, 10);

            var parPlateforme = await LireJsonAsync(await ListeAsync(_admin, $"?plateformeId={p1}"));
            var ids = parPlateforme.GetProperty("items").EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32()).ToList();
            Assert.Contains(attendu, ids);

            var parCategorie = await LireJsonAsync(await ListeAsync(_admin, "?categorie=Tissus"));
            Assert.All(parCategorie.GetProperty("items").EnumerateArray(),
                e => Assert.Equal("Tissus", e.GetProperty("articleCategorie").GetString()));
        }

        [Fact]
        public async Task Liste_PaginationEtTotal()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var article = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");
            for (var i = 0; i < 5; i++) await CreerStockAsync(article, plateforme, i + 1);

            var json = await LireJsonAsync(await ListeAsync(
                _admin, $"?plateformeId={plateforme}&taille=2&page=2"));

            Assert.Equal(2, json.GetProperty("items").GetArrayLength());
            Assert.Equal(5, json.GetProperty("total").GetInt32());
            Assert.Equal(3, json.GetProperty("pages").GetInt32());

            // Au-delà de la dernière page : page vide, pas une erreur.
            var auDessus = await ListeAsync(_admin, $"?plateformeId={plateforme}&taille=2&page=99");
            Assert.Equal(HttpStatusCode.OK, auDessus.StatusCode);
            Assert.Equal(0, (await LireJsonAsync(auDessus)).GetProperty("items").GetArrayLength());
        }

        [Fact]
        public async Task Liste_TypeStockInconnu_Retourne400()
        {
            var reponse = await ListeAsync(_admin, "?typeStock=Impossible");
            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        [Fact]
        public async Task Liste_AlertesOnly_NeRenvoieQueSousSeuil()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var article = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");

            await _factory.WithDbAsync(async ctx =>
            {
                var a = await ctx.Articles.FirstAsync(x => x.Id == article);
                a.SeuilAlerte = 10;
                a.SeuilCritique = 5;
                await ctx.SaveChangesAsync();
            });

            await CreerStockAsync(article, plateforme, 3);   // sous le seuil critique
            await CreerStockAsync(article, plateforme, 50);  // au-dessus

            var json = await LireJsonAsync(await ListeAsync(_admin, $"?plateformeId={plateforme}&alertesOnly=true"));
            var items = json.GetProperty("items").EnumerateArray().ToList();

            Assert.Single(items);
            Assert.Equal(3m, items[0].GetProperty("quantite").GetDecimal());
            Assert.True(items[0].GetProperty("enAlerte").GetBoolean());
            Assert.True(items[0].GetProperty("estCritique").GetBoolean());
        }

        // ── Partage : règle A et liste blanche ────────────────────────────

        [Fact]
        public async Task Partage_SectionStock_SansDroitStock_Retourne403()
        {
            // _partageur a PeutPartagerLiens mais pas PeutGererStock.
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);

            var reponse = await CreerLienAsync(_partageur.Client, new
            {
                scopeType = 1, // Plateforme
                scopeId = plateforme,
                sections = 1,  // Stock
                dureeHeures = 1,
                filters = new { },
            });

            Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
        }

        [Fact]
        public async Task Partage_SectionStock_AvecDroitStock_Accepte()
        {
            // Un utilisateur DEDICÉ, et non le rôle partagé : modifier le rôle de
            // _partageur laisserait fuiter le droit Stock vers
            // Partage_SectionStock_SansDroitStock_Retourne403 selon l'ordre xUnit.
            var avecDroit = await _factory.CreateUserAsync(
                "stock-liste-autorise", "Autorise", "Stock",
                r => { r.PeutPartagerLiens = true; r.PeutGererStock = true; });

            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);

            var reponse = await CreerLienAsync(avecDroit.Client, new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { },
            });

            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        }

        [Fact]
        public async Task Partage_FiltreArticleIds_NeRenvoieQueLaSelection()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var retenu = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");
            var exclu = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");

            var idRetenu = await CreerStockAsync(retenu, plateforme, 5);
            await CreerStockAsync(exclu, plateforme, 5);

            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { articleIds = new[] { retenu } },
            });
            var token = (await LireJsonAsync(reponse)).GetProperty("token").GetString()!;

            var ouvert = await OuvrirAsync(token);
            var ids = ouvert.GetProperty("stock").EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32()).ToList();

            Assert.Equal([idRetenu], ids);
        }

        [Fact]
        public async Task Partage_FiltreCategorieEtTypeStock_CombinésEnEt()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var tissus = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");
            var accessoires = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Accessoires");

            var attendu = await CreerStockAsync(tissus, plateforme, 5);
            await CreerStockAsync(tissus, plateforme, 5, TypeStock.Reserve);
            await CreerStockAsync(accessoires, plateforme, 5);

            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { categorie = "Tissus", typeStock = "Libre" },
            });
            var token = (await LireJsonAsync(reponse)).GetProperty("token").GetString()!;

            var ids = (await OuvrirAsync(token)).GetProperty("stock").EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32()).ToList();

            Assert.Equal([attendu], ids);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Partage_FiltreArticleIds_IdentifiantInvalide_Retourne400(int articleId)
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);

            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { articleIds = new[] { articleId } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        [Fact]
        public async Task Partage_FiltreArticleIds_Doublon_Retourne400()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var article = await CreerArticleAsync(Guid.NewGuid().ToString("N")[..6], "Tissus");

            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { articleIds = new[] { article, article } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        [Fact]
        public async Task Partage_FiltreArticleIds_ArticleInconnu_Retourne400()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);

            // Article inexistant : on refuse explicitement plutôt que de créer un
            // lien dont le filtre ne restreindrait rien.
            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { articleIds = new[] { 9_999_999 } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        [Fact]
        public async Task Partage_FiltreArticleIds_ArticleInactif_Retourne400()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var article = await CreerArticleAsync(
                Guid.NewGuid().ToString("N")[..6], "Tissus", actif: false);

            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { articleIds = new[] { article } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        [Fact]
        public async Task Partage_FiltreTypeStock_Inconnu_Retourne400()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);

            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { typeStock = "Bidon" },
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        [Fact]
        public async Task Partage_FiltreArticleIds_AuDessusDuPlafond_Retourne400()
        {
            var plateforme = await CreerPlateformeAsync(Guid.NewGuid().ToString("N")[..6]);
            var ids = Enumerable.Range(1, 201).ToArray();

            var reponse = await _admin.Client.PostAsJsonAsync("/api/partage", new
            {
                scopeType = 1,
                scopeId = plateforme,
                sections = 1,
                dureeHeures = 1,
                filters = new { articleIds = ids },
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }
    }
}
