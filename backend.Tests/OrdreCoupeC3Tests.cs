using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services.Coupe;
using Backend_Gestion_Magasin_API.Tests;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    /// <summary>
    /// LOT COUPE C3 — rattachement explicite des matelas (FK OrdreCoupeId), moteur
    /// unique (rapport et ordre de coupe = même calcul) et exports XLSX/PDF de
    /// l'ordre de coupe sans la ligne fixe « THERMO ».
    /// </summary>
    public class OrdreCoupeC3Tests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;
        private TestUser _auteur = null!;

        public OrdreCoupeC3Tests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _auteur = await _factory.CreateUserAsync(
                "c3-auteur", "Auteur", "Coupe",
                r => { r.PeutVoirCoupe = true; r.PeutGererCoupe = true; });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ───────────────────────────── Helpers ─────────────────────────────

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

        private async Task<int> CreerMatelasAsync(int commandeId, string numero)
        {
            return await _factory.WithDbAsync(async db =>
            {
                var m = new Matelas { CommandeId = commandeId, NumeroMatelas = numero, PiecePliage = 1, DateMatelas = DateTime.Now };
                db.Matelas.Add(m);
                await db.SaveChangesAsync();
                return m.Id;
            });
        }

        private async Task<int> CreerOrdreAsync(int commandeId, string modele, string couleur)
        {
            var res = await _auteur.Client.PostAsJsonAsync("/api/OrdreCoupe", new
            {
                CommandeId = commandeId, Modele = modele, Couleur = couleur,
            });
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        private async Task<int> AjouterTailleAsync(int id, string libelle, int? demande)
        {
            var res = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/tailles", new
            {
                Libelle = libelle, QuantiteDemandee = demande, QuantiteAvecMarge = (decimal?)null,
            });
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        private async Task AjouterPlanAsync(int id, int plis, int? matelasId, params (string Taille, int Occurrences)[] occ)
        {
            var res = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{id}/plans", new
            {
                Libelle = (string?)null,
                Plis = plis,
                MatelasId = matelasId,
                Occurrences = occ.Select(o => new { o.Taille, o.Occurrences }),
            });
            res.EnsureSuccessStatusCode();
        }

        private async Task<JsonElement> LireAsync(HttpResponseMessage res)
        {
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<JsonElement>();
        }

        // Construit un ordre complet à partir d'une feuille du corpus de référence.
        private async Task<int> ConstruireOrdreDepuisFixtureAsync(Feuille feuille)
        {
            var tailles = feuille.quantites.Select(kv => (kv.Key, kv.Value)).ToArray();
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, tailles);
            var ordreId = await CreerOrdreAsync(commandeId, feuille.modele, feuille.couleur);
            foreach (var t in feuille.tailles.Count > 0 ? feuille.tailles : feuille.quantites.Keys.ToList())
                await AjouterTailleAsync(ordreId, t, feuille.quantites.GetValueOrDefault(t));
            foreach (var passe in feuille.passes)
            {
                var occ = passe.occurrences.Select(kv => (kv.Key, kv.Value)).ToArray();
                await AjouterPlanAsync(ordreId, passe.plis, null, occ);
            }
            return ordreId;
        }

        private static Corpus ChargerCorpus()
        {
            var chemin = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ordre-coupe-2026.json");
            var json = File.ReadAllText(chemin);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            return JsonSerializer.Deserialize<Corpus>(json, options)
                   ?? throw new InvalidOperationException("Fixture de coupe illisible.");
        }

        // ───────────────────────────── 1. Rattachement ─────────────────────────────

        [Fact]
        public async Task Matelas_Orphelins_Listes_Puis_Rattaches_Et_Detaches()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var ordreId = await CreerOrdreAsync(commandeId, "C3A", "ECRU");
            var matelasId = await CreerMatelasAsync(commandeId, "MAT-C3-1");

            var orphelins = await LireAsync(await _auteur.Client.GetAsync($"/api/OrdreCoupe/commande/{commandeId}/matelas-orphelins"));
            Assert.Contains(orphelins.EnumerateArray(), m => m.GetProperty("matelasId").GetInt32() == matelasId);

            var attache = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{ordreId}/matelas", new { MatelasId = matelasId });
            attache.EnsureSuccessStatusCode();

            // Plus orphelin, et rattaché visible côté ordre + compteur.
            var orphelins2 = await LireAsync(await _auteur.Client.GetAsync($"/api/OrdreCoupe/commande/{commandeId}/matelas-orphelins"));
            Assert.DoesNotContain(orphelins2.EnumerateArray(), m => m.GetProperty("matelasId").GetInt32() == matelasId);

            var rattaches = await LireAsync(await _auteur.Client.GetAsync($"/api/OrdreCoupe/{ordreId}/matelas"));
            Assert.Contains(rattaches.EnumerateArray(), m => m.GetProperty("matelasId").GetInt32() == matelasId);

            var detail = await LireAsync(await _auteur.Client.GetAsync($"/api/OrdreCoupe/{ordreId}"));
            Assert.Equal(1, detail.GetProperty("nbMatelas").GetInt32());

            // Détachement : accepté tant qu'aucun plan ni coupe ne le référence.
            var detach = await _auteur.Client.DeleteAsync($"/api/OrdreCoupe/{ordreId}/matelas/{matelasId}");
            detach.EnsureSuccessStatusCode();

            var detail2 = await LireAsync(await _auteur.Client.GetAsync($"/api/OrdreCoupe/{ordreId}"));
            Assert.Equal(0, detail2.GetProperty("nbMatelas").GetInt32());
        }

        [Fact]
        public async Task Matelas_Reference_Par_Un_Plan_Est_Rattache_Automatiquement()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var ordreId = await CreerOrdreAsync(commandeId, "C3B", "ECRU");
            await AjouterTailleAsync(ordreId, "M", 10);
            var matelasId = await CreerMatelasAsync(commandeId, "MAT-C3-PLAN");

            await AjouterPlanAsync(ordreId, 1, matelasId, ("M", 1));

            var detail = await LireAsync(await _auteur.Client.GetAsync($"/api/OrdreCoupe/{ordreId}"));
            Assert.Equal(1, detail.GetProperty("nbMatelas").GetInt32());

            // Détachement refusé tant que le plan le référence.
            var detach = await _auteur.Client.DeleteAsync($"/api/OrdreCoupe/{ordreId}/matelas/{matelasId}");
            Assert.Equal(HttpStatusCode.Conflict, detach.StatusCode);
        }

        [Fact]
        public async Task Rattachement_Hors_Commande_Refuse()
        {
            var commandeA = await CreerCommandeAvecTaillesAsync(0m, ("M", 5));
            var commandeB = await CreerCommandeAvecTaillesAsync(0m, ("M", 5));
            var ordreA = await CreerOrdreAsync(commandeA, "C3C", "ECRU");
            var matelasB = await CreerMatelasAsync(commandeB, "MAT-C3-AUTRE");

            var res = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{ordreA}/matelas", new { MatelasId = matelasB });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task Matelas_Rattache_Deux_Fois_Refuse_Et_Verrou_Respecte()
        {
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, ("M", 10));
            var ordre1 = await CreerOrdreAsync(commandeId, "C3D1", "ECRU");
            var ordre2 = await CreerOrdreAsync(commandeId, "C3D2", "ECRU");
            var matelasId = await CreerMatelasAsync(commandeId, "MAT-C3-DOUBLE");

            (await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{ordre1}/matelas", new { MatelasId = matelasId }))
                .EnsureSuccessStatusCode();

            var second = await _auteur.Client.PostAsJsonAsync($"/api/OrdreCoupe/{ordre2}/matelas", new { MatelasId = matelasId });
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            // Verrou : une fois le matelas servi par une coupe, plus de détachement.
            await _factory.WithDbAsync(async db =>
            {
                db.LotCoupes.Add(new LotCoupe
                {
                    CommandeId = commandeId, MatelasId = matelasId, OrdreCoupeId = ordre1,
                    Taille = "M", QuantiteCoupee = 1,
                });
                await db.SaveChangesAsync();
            });
            var detach = await _auteur.Client.DeleteAsync($"/api/OrdreCoupe/{ordre1}/matelas/{matelasId}");
            Assert.Equal(HttpStatusCode.Conflict, detach.StatusCode);
        }

        // ───────────────────────────── 2. Moteur unique ─────────────────────────────

        [Theory]
        [InlineData("PDJ22-D676")]
        [InlineData("DDJ36-D128-MARINE")]
        [InlineData("OVITA")]
        public async Task Rapport_Et_OrdreDeCoupe_Derivent_Du_Meme_Moteur(string nomFeuille)
        {
            var feuille = ChargerCorpus().feuilles.Single(f => f.feuille == nomFeuille);
            var tailles = feuille.quantites.Select(kv => (kv.Key, kv.Value)).ToArray();
            var commandeId = await CreerCommandeAvecTaillesAsync(0m, tailles);

            // Le monde réel de la coupe : des matelas et leur plan de marquage.
            await _factory.WithDbAsync(async db =>
            {
                foreach (var passe in feuille.passes)
                {
                    var matelas = new Matelas
                    {
                        CommandeId = commandeId,
                        NumeroMatelas = "MAT-" + passe.numero,
                        PiecePliage = passe.plis,
                        DateMatelas = DateTime.Now.AddMinutes(passe.numero),
                    };
                    db.Matelas.Add(matelas);
                    await db.SaveChangesAsync();
                    foreach (var occ in passe.occurrences)
                        db.PlanDeCoupeLignes.Add(new PlanDeCoupeLigne
                        {
                            MatelasId = matelas.Id,
                            Taille = occ.Key,
                            Occurrences = occ.Value,
                        });
                    await db.SaveChangesAsync();
                }
            });

            // Référence : même entrée, calculée directement par le moteur C1.
            var (gabarits, passes) = CorpusVersTableau.Traduire(feuille);
            var attendu = MoteurCoupe.Calculer(gabarits, passes);

            var rapport = await LireAsync(await _auteur.Client.GetAsync($"/api/RapportCoupe/{commandeId}"));
            var ordreDeCoupe = await LireAsync(await _auteur.Client.GetAsync($"/api/RapportCoupe/{commandeId}/OrdreDeCoupe"));

            Assert.Equal(attendu.TotalPlanifie, rapport.GetProperty("totalPlanTheorique").GetInt64());
            Assert.Equal(attendu.TotalPlanifie, ordreDeCoupe.GetProperty("totalPlanTheorique").GetInt64());

            // Les deux vues partagent le même plan théorique par taille.
            var rapportParTaille = rapport.GetProperty("tailles").EnumerateArray()
                .ToDictionary(t => t.GetProperty("taille").GetString()!, t => t.GetProperty("planTheorique").GetInt32());
            var ordreParTaille = ordreDeCoupe.GetProperty("tailles").EnumerateArray()
                .ToDictionary(t => t.GetProperty("taille").GetString()!, t => t.GetProperty("planTheorique").GetInt32());
            Assert.Equal(rapportParTaille, ordreParTaille);

            // Et ce plan théorique par taille est exactement celui du moteur C1.
            var planMoteur = new Dictionary<string, int>();
            foreach (var passe in attendu.Passes)
                foreach (var (taille, contribution) in passe.Contributions)
                    planMoteur[taille] = planMoteur.GetValueOrDefault(taille) + contribution;
            foreach (var (taille, valeur) in planMoteur)
                Assert.Equal(valeur, rapportParTaille.GetValueOrDefault(taille));
        }

        // ───────────────────────────── 3. Exports XLSX / PDF ─────────────────────────────

        [Theory]
        [InlineData("PDJ22-D676")]
        [InlineData("DDJ36-D128-MARINE")]
        [InlineData("OVITA")]
        public async Task Export_Xlsx_Contient_Le_Resume_Et_Pas_De_Thermo(string nomFeuille)
        {
            var feuille = ChargerCorpus().feuilles.Single(f => f.feuille == nomFeuille);
            var ordreId = await ConstruireOrdreDepuisFixtureAsync(feuille);

            var res = await _auteur.Client.GetAsync($"/api/OrdreCoupe/{ordreId}/Export");
            res.EnsureSuccessStatusCode();
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", res.Content.Headers.ContentType?.MediaType);

            var bytes = await res.Content.ReadAsByteArrayAsync();
            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet(1);

            Assert.Equal(feuille.modele, ws.Cell(1, 2).GetString());
            Assert.Equal(feuille.couleur, ws.Cell(1, 4).GetString());
            // « QT » = quantité commandée (somme des tailles seedées) ; « QT COUPE » = coupes réelles (0 ici).
            Assert.Equal(feuille.quantites.Values.Sum(), ws.Cell(1, 10).GetValue<int>());
            Assert.Equal(0, ws.Cell(1, 12).GetValue<int>());

            // Aucune ligne « THERMO » codée en dur ne doit apparaître nulle part.
            var contientThermo = ws.CellsUsed()
                .Any(c => c.GetString().Contains("THERMO", StringComparison.OrdinalIgnoreCase));
            Assert.False(contientThermo, "L'export ne doit jamais contenir de ligne « THERMO » fixe.");
        }

        [Theory]
        [InlineData("PDJ22-D676")]
        [InlineData("OVITA")]
        public async Task Export_Pdf_Est_Un_Pdf_Valide(string nomFeuille)
        {
            var feuille = ChargerCorpus().feuilles.Single(f => f.feuille == nomFeuille);
            var ordreId = await ConstruireOrdreDepuisFixtureAsync(feuille);

            var res = await _auteur.Client.GetAsync($"/api/OrdreCoupe/{ordreId}/ExportPdf");
            res.EnsureSuccessStatusCode();
            Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);

            var bytes = await res.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 4);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public async Task Export_Refuse_Quand_Ordre_Inconnu()
        {
            var res = await _auteur.Client.GetAsync("/api/OrdreCoupe/999999/Export");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
    }
}
