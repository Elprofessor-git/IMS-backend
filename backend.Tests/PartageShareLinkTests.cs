using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    /// <summary>
    /// Lot « Partage sécurisé » : liens de lecture seule.
    /// </summary>
    /// <remarks>
    /// Ces tests exercent le VRAI pipeline : autorisation par rôle, résolution de
    /// périmètre sur base PostgreSQL migrée, ouverture anonyme et journalisation. Le
    /// point le plus important n'est pas « le lien marche » mais « il ne déborde
    /// jamais de son périmètre » : la majorité des cas vérifie une EXCLUSION.
    /// </remarks>
    public class PartageShareLinkTests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;
        private TestUser _admin = null!;
        private TestUser _partageur = null!;
        private TestUser _sansDroit = null!;

        public PartageShareLinkTests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _admin = await _factory.CreateUserAsync(
                "partage-admin", "Admin", "Partage",
                r => r.EstAdministrateur = true);

            _partageur = await _factory.CreateUserAsync(
                "partage-porteur", "Porteur", "Liens",
                r => r.PeutPartagerLiens = true);

            _sansDroit = await _factory.CreateUserAsync("partage-sansdroit", "Sans", "Droit");
        }

        public async Task DisposeAsync() { }

        // ── Helpers de données ────────────────────────────────────────────

        private sealed record Hierarchie(int PlateformeId, int ClientId, int CommandeId);

        private Task<Hierarchie> CreerHierarchieAsync(string suf) =>
            _factory.WithDbAsync(async ctx =>
            {
                var p = new Plateforme { Nom = "P-" + suf, EstActif = true };
                ctx.Plateformes.Add(p);
                await ctx.SaveChangesAsync();

                var c = new Client
                {
                    Nom = "C-" + suf,
                    Email = $"c.{Guid.NewGuid():N}@ims.test",
                    PlateformeId = p.Id,
                    EstActif = true
                };
                ctx.Clients.Add(c);
                await ctx.SaveChangesAsync();

                var k = new CommandeClient
                {
                    NumeroCommande = "CMD-" + suf + "-" + Guid.NewGuid().ToString("N")[..4],
                    ClientId = c.Id,
                    Statut = StatutCommande.EnProduction
                };
                ctx.CommandesClients.Add(k);
                await ctx.SaveChangesAsync();

                return new Hierarchie(p.Id, c.Id, k.Id);
            });

        private Task<int> CreerArticleAsync(string suf) =>
            _factory.WithDbAsync(async ctx =>
            {
                var a = new Article { Designation = "Article " + suf, Reference = "REF-" + suf };
                ctx.Articles.Add(a);
                await ctx.SaveChangesAsync();
                return a.Id;
            });

        private Task<int> CreerGroupeAsync(int commandeMembre) =>
            _factory.WithDbAsync(async ctx =>
            {
                var g = new GroupeCommande();
                ctx.GroupesCommandes.Add(g);
                await ctx.SaveChangesAsync();
                ctx.GroupeCommandeCommandes.Add(new GroupeCommandeCommande
                {
                    GroupeCommandeId = g.Id,
                    CommandeClientId = commandeMembre
                });
                await ctx.SaveChangesAsync();
                return g.Id;
            });

        private Task AjouterMembreGroupeAsync(int groupeId, int commandeId) =>
            _factory.WithDbAsync(async ctx =>
            {
                ctx.GroupeCommandeCommandes.Add(new GroupeCommandeCommande
                {
                    GroupeCommandeId = groupeId,
                    CommandeClientId = commandeId
                });
                await ctx.SaveChangesAsync();
            });

        private Task<int> CreerStockAsync(int articleId, decimal quantite, Action<Stock> scope) =>
            _factory.WithDbAsync(async ctx =>
            {
                var s = new Stock { ArticleId = articleId, Quantite = quantite };
                scope(s);
                ctx.Stocks.Add(s);
                await ctx.SaveChangesAsync();
                return s.Id;
            });

        private Task<int> CreerImportationAvecLigneAsync(Action<LigneImportation> configure) =>
            _factory.WithDbAsync(async ctx =>
            {
                var importation = new Importation
                {
                    ReferenceImportation = "IMP-" + Guid.NewGuid().ToString("N")[..6],
                    Statut = StatutImportation.Soumise
                };
                ctx.Importations.Add(importation);
                await ctx.SaveChangesAsync();

                var article = new Article { Designation = "Art import" };
                ctx.Articles.Add(article);
                await ctx.SaveChangesAsync();

                var ligne = new LigneImportation
                {
                    ImportationId = importation.Id,
                    ArticleId = article.Id,
                    Quantite = 5,
                    QuantiteRecue = 0,
                };
                configure(ligne);
                ctx.LignesImportation.Add(ligne);
                await ctx.SaveChangesAsync();
                return ligne.Id;
            });

        // ── Helpers d'appel ───────────────────────────────────────────────

        private static Task<HttpResponseMessage> CreerLienAsync(HttpClient client, object dto) =>
            client.PostAsJsonAsync("/api/Partage", dto);

        private static async Task<string> TokenDeAsync(HttpResponseMessage reponse)
        {
            var json = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("token").GetString()!;
        }

        /// <summary>Ouvre un lien avec l'IP fournie (X-Forwarded-For), sans authentification.</summary>
        private async Task<HttpResponseMessage> OuvrirAsync(string token, string ip)
        {
            var requete = new HttpRequestMessage(HttpMethod.Post, "/api/Partage/Public")
            {
                Content = JsonContent.Create(new { token })
            };
            requete.Headers.Add("X-Forwarded-For", ip);
            return await _factory.CreateClient().SendAsync(requete);
        }

        private async Task<JsonElement> OuvrirJsonAsync(string token, string ip)
        {
            var reponse = await OuvrirAsync(token, ip);
            reponse.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await reponse.Content.ReadAsStringAsync()).RootElement;
        }

        private static HashSet<int> IdsDe(JsonElement racine, string section)
        {
            var element = racine.GetProperty(section);
            if (element.ValueKind == JsonValueKind.Null) return [];
            return element.EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32())
                .ToHashSet();
        }

        // ── Autorisation ──────────────────────────────────────────────────

        [Fact]
        public async Task Creer_SansDroit_Retourne403()
        {
            var reponse = await CreerLienAsync(_sansDroit.Client, new
            {
                scopeType = 3, scopeId = 1, sections = 1, dureeHeures = 1
            });
            Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
        }

        [Fact]
        public async Task Creer_SectionsVides_Retourne400()
        {
            var h = await CreerHierarchieAsync("sec");
            var reponse = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 0, dureeHeures = 1
            });
            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        [Fact]
        public async Task Creer_ScopeInconnu_Retourne400()
        {
            var reponse = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = 999_999_999, sections = 1, dureeHeures = 1
            });
            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        // ── Token : jamais stocké en clair, absent de la vue admin ─────────

        [Fact]
        public async Task Creer_TokenRetourneUneFois_MaisSeuleEmpreinteEnBase()
        {
            var h = await CreerHierarchieAsync("hash");
            var reponse = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 1, dureeHeures = 1
            });
            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
            var token = await TokenDeAsync(reponse);
            Assert.False(string.IsNullOrWhiteSpace(token));

            var bodyReponse = await reponse.Content.ReadAsStringAsync();
            var vue = JsonDocument.Parse(bodyReponse).RootElement.GetProperty("link");
            var lienId = vue.GetProperty("id").GetInt32();

            var (hashStocke, tokenDansVue) = await _factory.WithDbAsync(async ctx =>
            {
                var lien = await ctx.ShareLinks.SingleAsync(l => l.Id == lienId);
                return (lien.TokenHash, vue.TryGetProperty("token", out _));
            });

            // L'empreinte est le SHA-256 attendu du token, pas le token.
            Assert.Equal(ShareTokenHash(token), hashStocke);
            Assert.DoesNotContain(token, hashStocke, StringComparison.Ordinal);
            // La vue administrateur ne porte AUCUN token.
            Assert.False(tokenDansVue);

            // La liste ne réexpose pas non plus le token.
            var liste = await _admin.Client.GetAsync("/api/Partage");
            liste.EnsureSuccessStatusCode();
            Assert.DoesNotContain(token, await liste.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        private static string ShareTokenHash(string token)
        {
            var octets = System.Text.Encoding.UTF8.GetBytes(token);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(octets)).ToLowerInvariant();
        }

        // ── Périmètre : le cœur de la sécurité ────────────────────────────

        [Fact]
        public async Task Public_Plateforme_InclutSaHierarchie_EtRienDautre()
        {
            var p1 = await CreerHierarchieAsync("p1");
            var p2 = await CreerHierarchieAsync("p2");
            var article = await CreerArticleAsync("plateforme");

            // Groupe entièrement dans p1, et groupe à cheval p1/p2.
            var groupeInterne = await CreerGroupeAsync(p1.CommandeId);
            var groupeCheval = await CreerGroupeAsync(p1.CommandeId);
            await AjouterMembreGroupeAsync(groupeCheval, p2.CommandeId);

            var sPlateformeDans = await CreerStockAsync(article, 1, s => s.PlateformeId = p1.PlateformeId);
            var sPlateformeHors = await CreerStockAsync(article, 1, s => s.PlateformeId = p2.PlateformeId);
            var sClient = await CreerStockAsync(article, 1, s => s.ClientId = p1.ClientId);
            var sCommande = await CreerStockAsync(article, 1, s => s.CommandeClientId = p1.CommandeId);
            var sGroupeInterne = await CreerStockAsync(article, 1, s => s.GroupeCommandeId = groupeInterne);
            var sGroupeCheval = await CreerStockAsync(article, 1, s => s.GroupeCommandeId = groupeCheval);
            var sLibre = await CreerStockAsync(article, 1, _ => { });

            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 1, scopeId = p1.PlateformeId, sections = 1,
                dureeHeures = 1, isStockLibreAllowed = false
            });
            creation.EnsureSuccessStatusCode();
            var racine = await OuvrirJsonAsync(await TokenDeAsync(creation), "10.1.0.1");

            var ids = IdsDe(racine, "stock");
            Assert.Contains(sPlateformeDans, ids);
            Assert.Contains(sClient, ids);
            Assert.Contains(sCommande, ids);
            Assert.Contains(sGroupeInterne, ids);

            Assert.DoesNotContain(sPlateformeHors, ids); // autre plateforme
            Assert.DoesNotContain(sGroupeCheval, ids);   // groupe à cheval : exclu
            Assert.DoesNotContain(sLibre, ids);          // stock libre non autorisé

            // Sections non demandées → null, pas tableau vide.
            Assert.Equal(JsonValueKind.Null, racine.GetProperty("commandes").ValueKind);
            Assert.Equal(JsonValueKind.Null, racine.GetProperty("importations").ValueKind);
        }

        [Fact]
        public async Task Public_Marque_ExclutLeStockDeLaPlateformeParente()
        {
            var h = await CreerHierarchieAsync("marque");
            var article = await CreerArticleAsync("marque");

            var sPlateforme = await CreerStockAsync(article, 1, s => s.PlateformeId = h.PlateformeId);
            var sClient = await CreerStockAsync(article, 1, s => s.ClientId = h.ClientId);
            var sCommande = await CreerStockAsync(article, 1, s => s.CommandeClientId = h.CommandeId);

            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 2, scopeId = h.ClientId, sections = 1, dureeHeures = 1
            });
            creation.EnsureSuccessStatusCode();
            var racine = await OuvrirJsonAsync(await TokenDeAsync(creation), "10.1.0.2");

            var ids = IdsDe(racine, "stock");
            Assert.Contains(sClient, ids);
            Assert.Contains(sCommande, ids);
            // Le stock rattaché directement à la plateforme parente n'appartient pas
            // à la marque : il ne doit PAS apparaître.
            Assert.DoesNotContain(sPlateforme, ids);
        }

        [Fact]
        public async Task Public_Commande_IsoleLaCommande()
        {
            var h = await CreerHierarchieAsync("commande");
            var article = await CreerArticleAsync("commande");

            var sClient = await CreerStockAsync(article, 1, s => s.ClientId = h.ClientId);
            var sCommande = await CreerStockAsync(article, 1, s => s.CommandeClientId = h.CommandeId);

            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 1, dureeHeures = 1
            });
            creation.EnsureSuccessStatusCode();
            var racine = await OuvrirJsonAsync(await TokenDeAsync(creation), "10.1.0.3");

            var ids = IdsDe(racine, "stock");
            Assert.Contains(sCommande, ids);
            Assert.DoesNotContain(sClient, ids);
        }

        [Fact]
        public async Task Public_StockLibre_InclusSeulementSiAutorise()
        {
            var h = await CreerHierarchieAsync("libre");
            var article = await CreerArticleAsync("libre");
            var sLibre = await CreerStockAsync(article, 1, _ => { });

            var refuse = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 1, dureeHeures = 1
            });
            var racineRefus = await OuvrirJsonAsync(await TokenDeAsync(refuse), "10.1.0.4");
            Assert.DoesNotContain(sLibre, IdsDe(racineRefus, "stock"));

            var autorise = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 1, dureeHeures = 1,
                isStockLibreAllowed = true
            });
            var racineOk = await OuvrirJsonAsync(await TokenDeAsync(autorise), "10.1.0.5");
            Assert.Contains(sLibre, IdsDe(racineOk, "stock"));
        }

        [Fact]
        public async Task Public_Importations_ScopingParTypeDestination_EtFkResiduelleIgnoree()
        {
            var h = await CreerHierarchieAsync("imp");
            var horsPerimetre = await CreerHierarchieAsync("imp-hors");

            // Ligne « destination commande » dans le périmètre.
            var ligneCommande = await CreerImportationAvecLigneAsync(l =>
            {
                l.TypeDestination = TypeDestinationImportation.Commande;
                l.CommandeClientId = h.CommandeId;
            });

            // Ligne « destination plateforme » HORS périmètre, MAIS portant en résiduel
            // le ClientId de la marque dans le périmètre : la FK résiduelle ne doit pas
            // suffire à la faire entrer. Le TypeDestination fait foi, pas la FK.
            var ligneResiduelle = await CreerImportationAvecLigneAsync(l =>
            {
                l.TypeDestination = TypeDestinationImportation.Plateforme;
                l.PlateformeId = horsPerimetre.PlateformeId; // réelle, mais hors périmètre
                l.ClientId = h.ClientId;                     // résidu non concordant
            });

            // Ligne « destination marque » dans le périmètre (ClientId concordant).
            var ligneMarque = await CreerImportationAvecLigneAsync(l =>
            {
                l.TypeDestination = TypeDestinationImportation.Marque;
                l.ClientId = h.ClientId;
            });

            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 4, dureeHeures = 1
            });
            creation.EnsureSuccessStatusCode();
            var racine = await OuvrirJsonAsync(await TokenDeAsync(creation), "10.1.0.6");

            var ids = IdsDe(racine, "importations");
            Assert.Contains(ligneCommande, ids);
            Assert.DoesNotContain(ligneResiduelle, ids);
            // La ligne « marque » n'est pas dans le périmètre d'un lien de commande.
            Assert.DoesNotContain(ligneMarque, ids);
        }

        [Fact]
        public async Task Public_NeDivulgueAucuneDonneeCommerciale()
        {
            // Suffixe NEUTRE : il ne doit contenir aucun des mots interdits, sinon un
            // simple nom (« Article prix ») ferait échouer le test sur du bruit.
            var h = await CreerHierarchieAsync("divulgation");
            var article = await CreerArticleAsync("divulgation");
            await CreerStockAsync(article, 1, s =>
            {
                s.CommandeClientId = h.CommandeId;
                s.PrixUnitaire = 1234.56m;
                s.PrixUnitaireTND = 4200m;
                s.Notes = "interne confidentiel";
            });

            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 7, dureeHeures = 1
            });
            creation.EnsureSuccessStatusCode();
            var brut = await (await OuvrirAsync(await TokenDeAsync(creation), "10.1.0.7"))
                .Content.ReadAsStringAsync();

            // On cherche des NOMS DE CHAMP, pas des mots : « prixUnitaire », « montant »…
            foreach (var interdit in new[]
                     {
                         "prixUnitaire", "prix", "montant", "fournisseur",
                         "devise", "notes", "validePar", "chemin"
                     })
                Assert.DoesNotContain(interdit, brut, StringComparison.OrdinalIgnoreCase);
        }

        // ── Invalidation : un 404 indiscernable ───────────────────────────

        [Fact]
        public async Task Public_TokenInconnu_Et_TokenRevoque_SontIndiscernables()
        {
            var h = await CreerHierarchieAsync("revoke");
            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 1, dureeHeures = 1
            });
            creation.EnsureSuccessStatusCode();
            var creationJson = JsonDocument.Parse(await creation.Content.ReadAsStringAsync()).RootElement;
            var token = creationJson.GetProperty("token").GetString()!;
            var idLien = creationJson.GetProperty("link").GetProperty("id").GetInt32();

            var inconnu = await OuvrirAsync("token-inexistant-000", "10.2.0.1");
            Assert.Equal(HttpStatusCode.NotFound, inconnu.StatusCode);

            // Révocation par un tiers non administrateur : lien inconnu pour lui → 404.
            var tiers = await _factory.CreateUserAsync("partage-tiers", "Tiers", "Test",
                r => r.PeutPartagerLiens = true);

            var deleteTiers = await tiers.Client.DeleteAsync($"/api/Partage/{idLien}");
            Assert.Equal(HttpStatusCode.NotFound, deleteTiers.StatusCode);
            // Toujours ouvrable : le tiers n'a rien révoqué.
            Assert.Equal(HttpStatusCode.OK, (await OuvrirAsync(token, "10.2.0.3")).StatusCode);

            // Révocation par le propriétaire : 204, puis 404 à l'ouverture.
            var deleteProprietaire = await _admin.Client.DeleteAsync($"/api/Partage/{idLien}");
            Assert.Equal(HttpStatusCode.NoContent, deleteProprietaire.StatusCode);

            var apresRevocation = await OuvrirAsync(token, "10.2.0.4");
            Assert.Equal(HttpStatusCode.NotFound, apresRevocation.StatusCode);
        }

        [Fact]
        public async Task Public_TokenExpire_Retourne404()
        {
            var h = await CreerHierarchieAsync("expire");
            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 1, dureeHeures = 1
            });
            creation.EnsureSuccessStatusCode();
            var creationJson = JsonDocument.Parse(await creation.Content.ReadAsStringAsync()).RootElement;
            var token = creationJson.GetProperty("token").GetString()!;
            var idLien = creationJson.GetProperty("link").GetProperty("id").GetInt32();

            await _factory.WithDbAsync(async ctx =>
            {
                var lien = await ctx.ShareLinks.SingleAsync(l => l.Id == idLien);
                lien.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
                await ctx.SaveChangesAsync();
            });

            Assert.Equal(HttpStatusCode.NotFound, (await OuvrirAsync(token, "10.2.0.5")).StatusCode);
        }

        [Fact]
        public async Task Public_MaxUsesAtteint_Retourne404()
        {
            var h = await CreerHierarchieAsync("maxuses");
            var creation = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h.CommandeId, sections = 1, dureeHeures = 1,
                maxUses = 1
            });
            var token = await TokenDeAsync(creation);

            Assert.Equal(HttpStatusCode.OK, (await OuvrirAsync(token, "10.2.0.6")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await OuvrirAsync(token, "10.2.0.7")).StatusCode);
        }

        // ── Liste : cloisonnement propriétaire / admin ────────────────────

        [Fact]
        public async Task Liste_NonAdmin_VoitSesLiens_LAdminVoitTout()
        {
            var h1 = await CreerHierarchieAsync("list1");
            var h2 = await CreerHierarchieAsync("list2");

            var lienPorteur = await CreerLienAsync(_partageur.Client, new
            {
                scopeType = 3, scopeId = h1.CommandeId, sections = 1, dureeHeures = 1
            });
            lienPorteur.EnsureSuccessStatusCode();
            var lienAdmin = await CreerLienAsync(_admin.Client, new
            {
                scopeType = 3, scopeId = h2.CommandeId, sections = 1, dureeHeures = 1
            });
            lienAdmin.EnsureSuccessStatusCode();

            var vuePorteur = await IdsLiensAsync(await _partageur.Client.GetAsync("/api/Partage"));
            var vueAdmin = await IdsLiensAsync(await _admin.Client.GetAsync("/api/Partage"));

            var idPorteur = await IdLienAsync("list1");
            var idAdmin = await IdLienAsync("list2");

            Assert.Contains(idPorteur, vuePorteur);
            Assert.DoesNotContain(idAdmin, vuePorteur);
            Assert.Contains(idPorteur, vueAdmin);
            Assert.Contains(idAdmin, vueAdmin);
        }

        private async Task<int> IdLienAsync(string suffixe)
        {
            // Le NumeroCommande contient le suffixe : on retrouve le lien par sa portée.
            return await _factory.WithDbAsync(async ctx =>
            {
                var commandeId = (await ctx.CommandesClients.ToListAsync())
                    .First(c => c.NumeroCommande.Contains("-" + suffixe + "-")).Id;
                return (await ctx.ShareLinks.ToListAsync())
                    .First(l => l.ScopeId == commandeId).Id;
            });
        }

        private static async Task<HashSet<int>> IdsLiensAsync(HttpResponseMessage reponse)
        {
            reponse.EnsureSuccessStatusCode();
            var json = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync());
            return json.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32())
                .ToHashSet();
        }

        // ── Anti-régression du flag de rôle ───────────────────────────────

        [Fact]
        public async Task Role_GetPuisPut_NePerdPasPeutPartagerLiens()
        {
            // Le PUT des rôles est un REMPLACEMENT complet : si le champ manquait d'un
            // côté de l'aller-retour GET → PUT, enregistrer un rôle révoquerait
            // silencieusement le droit. Ce test verrouille la propriété.
            var creation = await _admin.Client.PostAsJsonAsync("/api/roles", new
            {
                name = "RolePartage_" + Guid.NewGuid().ToString("N")[..6],
                peutPartagerLiens = true,
            });
            creation.EnsureSuccessStatusCode();
            var cree = JsonDocument.Parse(await creation.Content.ReadAsStringAsync()).RootElement;
            var roleId = cree.GetProperty("id").GetInt32();
            Assert.True(cree.GetProperty("peutPartagerLiens").GetBoolean());

            var get = await _admin.Client.GetAsync($"/api/roles/{roleId}");
            get.EnsureSuccessStatusCode();
            var corps = await get.Content.ReadAsStringAsync();
            Assert.True(JsonDocument.Parse(corps).RootElement.GetProperty("peutPartagerLiens").GetBoolean());

            // PUT du corps relu tel quel (aller-retour sans perte).
            var put = await _admin.Client.PutAsJsonAsync(
                $"/api/roles/{roleId}", JsonDocument.Parse(corps).RootElement);
            Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

            var relu = JsonDocument.Parse(
                await (await _admin.Client.GetAsync($"/api/roles/{roleId}")).Content.ReadAsStringAsync());
            Assert.True(relu.RootElement.GetProperty("peutPartagerLiens").GetBoolean());
        }
    }
}