using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    public class OrdreCoupeC2Tests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;
        private TestUser _auteur = null!;
        private TestUser _lecteur = null!;
        private TestUser _etranger = null!;

        public OrdreCoupeC2Tests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _auteur = await _factory.CreateUserAsync(
                "c2-auteur", "Auteur", "Coupe",
                r => { r.PeutVoirCoupe = true; r.PeutGererCoupe = true; });

            _lecteur = await _factory.CreateUserAsync(
                "c2-lecteur", "Lecteur", "Coupe",
                r => { r.PeutVoirCoupe = true; r.PeutGererCoupe = false; });

            _etranger = await _factory.CreateUserAsync(
                "c2-etranger", "Etranger", "Aucun",
                _ => { });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<int> CreerCommandeAvecTaillesAsync(decimal marge, params (string Taille, int Quantite)[] tailles)
        {
            var commandeId = await _factory.CreateCommandeClientAsync();
            await _factory.WithDbAsync(async db =>
            {
                var commande = await db.CommandesClients.FirstAsync(c => c.Id == commandeId);
                commande.MargeSecuriteDefaut = marge;
                foreach (var t in tailles)
                    db.ConfigTailles.Add(new ConfigTaille { CommandeId = commandeId, Taille = t.Taille, Quantite = t.Quantite });
                await db.SaveChangesAsync();
            });
            return commandeId;
        }

        private static async Task<int> CreerOrdreAsync(HttpClient client, int commandeId, string modele, string couleur, int? chaineId = null)
        {
            var res = await client.PostAsJsonAsync("/api/OrdreCoupe", new
            {
                CommandeId = commandeId,
                Modele = modele,
                Couleur = couleur,
                ChaineProductionId = chaineId,
            });
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("id").GetInt32();
        }

        private static async Task<JsonElement> LireAsync(HttpResponseMessage res)
        {
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<JsonElement>();
        }

        private static async Task<JsonElement> GetOrdreAsync(HttpClient client, int id)
            => await LireAsync(await client.GetAsync($"/api/OrdreCoupe/{id}"));

        private static async Task<int> PreremplirAsync(HttpClient client, int id)
        {
            var res = await client.PostAsync($"/api/OrdreCoupe/{id}/Preremplir", null);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tailles").GetArrayLength();
        }

        private static async Task<int> AjouterTailleAsync(HttpClient client, int id, string libelle, int? demande = null, decimal? avecMarge = null)
        {
            var res = await client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/tailles", new
            {
                Libelle = libelle,
                QuantiteDemandee = demande,
                QuantiteAvecMarge = avecMarge,
            });
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        private static async Task<int> AjouterPlanAsync(HttpClient client, int id, int plis, params (string Taille, int Occurrences)[] occurrences)
        {
            var res = await client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/plans", new
            {
                Libelle = (string?)null,
                Plis = plis,
                Occurrences = occurrences.Select(o => new { o.Taille, o.Occurrences }),
            });
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        private static JsonElement TailleParLibelle(JsonElement ordre, string libelle)
        {
            foreach (var t in ordre.GetProperty("tailles").EnumerateArray())
                if (t.GetProperty("libelle").GetString() == libelle)
                    return t;
            throw new Xunit.Sdk.XunitException($"Taille '{libelle}' absente de la réponse.");
        }

        [Fact]
        public async Task Creer_Ordre_Vide_Ne_Devine_Aucune_Cellule()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(10m, ("M", 100));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");

            var ordre = await GetOrdreAsync(_auteur.Client, id);
            Assert.Equal(0, ordre.GetProperty("tailles").GetArrayLength());
            Assert.Equal(0, ordre.GetProperty("plans").GetArrayLength());
            Assert.Equal(0, ordre.GetProperty("totalCommande").GetInt64());
        }

        [Fact]
        public async Task Creer_Ordre_Doublon_Modele_Couleur_Refuse()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m);
            await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");

            var res = await _auteur.Client.PostAsJsonAsync("/api/OrdreCoupe", new
            {
                CommandeId = commandeId,
                Modele = "pdj22 ",
                Couleur = "ecru",
            });

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }

        [Fact]
        public async Task Creer_Ordre_Chaine_Inconnue_Refuse()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m);
            var res = await _auteur.Client.PostAsJsonAsync("/api/OrdreCoupe", new
            {
                CommandeId = commandeId,
                Modele = "PDJ22",
                Couleur = "ECRU",
                ChaineProductionId = 999_999,
            });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task Ecriture_Refusee_Sans_Droit_Gerer_Coupe()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m);

            var res = await _lecteur.Client.PostAsJsonAsync("/api/OrdreCoupe", new
            {
                CommandeId = commandeId,
                Modele = "PDJ22",
                Couleur = "ECRU",
            });

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        [Fact]
        public async Task Lecture_Refusee_Sans_Acces_Module_Coupe()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m);
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");

            var res = await _etranger.Client.GetAsync($"/api/OrdreCoupe/{id}");

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        [Fact]
        public async Task Preremplir_Remplit_Depuis_Les_Besoins_De_La_Commande()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(10m, ("M", 100), ("L", 50));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");

            await PreremplirAsync(_auteur.Client, id);

            var ordre = await GetOrdreAsync(_auteur.Client, id);
            Assert.Equal(2, ordre.GetProperty("tailles").GetArrayLength());

            var m = TailleParLibelle(ordre, "M");
            Assert.Equal(100, m.GetProperty("quantiteDemandee").GetInt32());
            Assert.Equal(110m, m.GetProperty("quantiteAvecMarge").GetDecimal());

            var l = TailleParLibelle(ordre, "L");
            Assert.Equal(55m, l.GetProperty("quantiteAvecMarge").GetDecimal());
        }

        [Fact]
        public async Task Preremplir_N_Ecrase_Jamais_Une_Surcharge_Manuelle()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(10m, ("M", 100));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");
            await PreremplirAsync(_auteur.Client, id);

            var ordre = await GetOrdreAsync(_auteur.Client, id);
            var tailleId = TailleParLibelle(ordre, "M").GetProperty("id").GetInt32();

            var put = await _auteur.Client.PutAsJsonAsync($"/api/OrdreCoupe/tailles/{tailleId}", new
            {
                Libelle = "M",
                QuantiteDemandee = 7,
                QuantiteAvecMarge = 9m,
            });
            put.EnsureSuccessStatusCode();

            await PreremplirAsync(_auteur.Client, id);

            var apres = TailleParLibelle(await GetOrdreAsync(_auteur.Client, id), "M");
            Assert.Equal(7, apres.GetProperty("quantiteDemandee").GetInt32());
            Assert.Equal(9m, apres.GetProperty("quantiteAvecMarge").GetDecimal());
        }

        [Fact]
        public async Task Preremplir_Conserve_La_Marge_Personnalisee_De_L_Ordre()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(10m, ("M", 100));
            var res = await _auteur.Client.PostAsJsonAsync("/api/OrdreCoupe", new
            {
                CommandeId = commandeId,
                Modele = "PDJ22",
                Couleur = "ECRU",
                MargeSecurite = 20m,
            });
            var id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            await PreremplirAsync(_auteur.Client, id);

            var m = TailleParLibelle(await GetOrdreAsync(_auteur.Client, id), "M");
            Assert.Equal(120m, m.GetProperty("quantiteAvecMarge").GetDecimal());
        }

        [Fact]
        public async Task Restes_Negatifs_Et_Surplus_Calcules_Par_Le_Moteur()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");
            await PreremplirAsync(_auteur.Client, id);
            await AjouterPlanAsync(_auteur.Client, id, 5, ("M", 4));

            var ordre = await GetOrdreAsync(_auteur.Client, id);
            Assert.Equal(20, ordre.GetProperty("totalPlanifie").GetInt64());
            Assert.Equal(10, ordre.GetProperty("surplus").GetInt64());
            Assert.Equal(0, ordre.GetProperty("manque").GetInt64());
            Assert.False(ordre.GetProperty("alerteManque").GetBoolean());

            var restes = ordre.GetProperty("plans")[0].GetProperty("restes");
            Assert.Equal(-10, restes[0].GetProperty("valeur").GetInt32());
        }

        [Fact]
        public async Task Manque_Et_Alerte_Quand_La_Commande_N_Est_Pas_Couverte()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 100));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");
            await PreremplirAsync(_auteur.Client, id);
            await AjouterPlanAsync(_auteur.Client, id, 1, ("M", 1));

            var ordre = await GetOrdreAsync(_auteur.Client, id);
            Assert.Equal(1, ordre.GetProperty("totalPlanifie").GetInt64());
            Assert.Equal(99, ordre.GetProperty("manque").GetInt64());
            Assert.True(ordre.GetProperty("alerteManque").GetBoolean());
        }

        [Fact]
        public async Task Matiere_Alerte_Tissu_Quand_Le_Stock_Restant_Est_Negatif()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");
            await PreremplirAsync(_auteur.Client, id);
            await AjouterPlanAsync(_auteur.Client, id, 10, ("M", 1));

            var res = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/matieres", new
            {
                Designation = "Tissu jean",
                Laize = 150m,
                ConsoClient = 1m,
                RetraitPourcentage = 0m,
                MetresRecus = 5m,
            });
            res.EnsureSuccessStatusCode();

            var matiere = (await GetOrdreAsync(_auteur.Client, id)).GetProperty("matieres")[0];
            Assert.Equal(10m, matiere.GetProperty("utilises").GetDecimal());
            Assert.Equal(-5m, matiere.GetProperty("stockRestant").GetDecimal());
            Assert.True(matiere.GetProperty("alerte").GetBoolean());
        }

        [Fact]
        public async Task Matiere_Expose_L_Ecart_Entre_Conso_Client_Et_Conso_Reelle()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");

            var res = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/matieres", new
            {
                Designation = "Doublure",
                ConsoClient = 1.2m,
                ConsoReelle = 1.35m,
                RetraitPourcentage = 5m,
                MetresRecus = 100m,
            });
            res.EnsureSuccessStatusCode();

            var matiere = (await GetOrdreAsync(_auteur.Client, id)).GetProperty("matieres")[0];
            Assert.Equal(0.15m, matiere.GetProperty("ecartConso").GetDecimal());
            Assert.Equal(5m, matiere.GetProperty("retrait").GetDecimal());
        }

        [Fact]
        public async Task Verrou_Premiere_Coupe_Fige_Le_Plan()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");
            await PreremplirAsync(_auteur.Client, id);

            var matelasId = await _factory.WithDbAsync(async db =>
            {
                var m = new Matelas { CommandeId = commandeId, NumeroMatelas = "MAT-C2", PiecePliage = 1 };
                db.Matelas.Add(m);
                await db.SaveChangesAsync();
                return m.Id;
            });

            var resPlan = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/plans", new
            {
                Libelle = "nappe 1",
                Plis = 1,
                MatelasId = matelasId,
                Occurrences = new[] { new { Taille = "M", Occurrences = 1 } },
            });
            resPlan.EnsureSuccessStatusCode();
            var planId = (await resPlan.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            await _factory.WithDbAsync(async db =>
            {
                db.LotCoupes.Add(new LotCoupe
                {
                    CommandeId = commandeId,
                    MatelasId = matelasId,
                    Taille = "M",
                    QuantiteCoupee = 1,
                });
                await db.SaveChangesAsync();
            });

            Assert.True((await GetOrdreAsync(_auteur.Client, id)).GetProperty("verrouille").GetBoolean());

            var maj = await _auteur.Client.PutAsJsonAsync($"/api/OrdreCoupe/plans/{planId}", new
            {
                Libelle = "nappe 1",
                Plis = 2,
                MatelasId = matelasId,
                Occurrences = new[] { new { Taille = "M", Occurrences = 1 } },
            });
            Assert.Equal(HttpStatusCode.Conflict, maj.StatusCode);

            var suppression = await _auteur.Client.DeleteAsync($"/api/OrdreCoupe/plans/{planId}");
            Assert.Equal(HttpStatusCode.Conflict, suppression.StatusCode);
        }

        [Fact]
        public async Task Plan_Taille_Absente_Des_Colonnes_Refuse()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU");
            await PreremplirAsync(_auteur.Client, id);

            var res = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/plans", new
            {
                Plis = 1,
                Occurrences = new[] { new { Taille = "ZZ", Occurrences = 1 } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task Chaine_De_Production_Selectionnable_Et_Exposee()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var chaineId = await _factory.WithDbAsync(async db =>
            {
                var c = new ChaineProduction
                {
                    Nom = "Atelier interne C2",
                    TypeChaine = TypeChaineProduction.Confection,
                    EstSousTraitant = false,
                    EstActif = true,
                };
                db.ChainesProduction.Add(c);
                await db.SaveChangesAsync();
                return c.Id;
            });

            var id = await CreerOrdreAsync(_auteur.Client, commandeId, "PDJ22", "ECRU", chaineId);

            var ordre = await GetOrdreAsync(_auteur.Client, id);
            Assert.Equal(chaineId, ordre.GetProperty("chaineProductionId").GetInt32());
            Assert.Equal("Atelier interne C2", ordre.GetProperty("chaineProductionNom").GetString());
        }
    }
}
