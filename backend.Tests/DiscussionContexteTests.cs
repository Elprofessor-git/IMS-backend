using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    /// <summary>
    /// Module Discussion — étape E3 : discussions CONTEXTUELLES rattachées à un objet
    /// métier (commande, OF, tâche, achat), avec cartes de références calculées selon
    /// les droits du LECTEUR.
    ///
    /// Les deux invariants non négociables vérifiés ici :
    /// <list type="bullet">
    /// <item>un utilisateur sans le module de l'objet n'ouvre PAS la discussion
    /// contextuelle (403) et ne reçoit AUCUNE carte pour cet objet ;</item>
    /// <item>une carte ne porte de montant ni de tiers que si le lecteur a le droit
    /// d'ÉCRITURE du module — jamais un prix « masqué côté client ».</item>
    /// </list>
    /// </summary>
    public class DiscussionContexteTests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;

        // Détient commandes + achats en ÉCRITURE, tâches (responsable).
        private TestUser _auteur = null!;

        // Détient commandes + achats en simple LECTURE (pas de droit d'écriture).
        private TestUser _lecteur = null!;

        // Détient le drapeau Discussion, mais AUCUN module métier.
        private TestUser _tiers = null!;

        public DiscussionContexteTests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _auteur = await _factory.CreateUserAsync(
                "ctx-auteur", "Auteur", "Ecriture", r =>
                {
                    r.PeutUtiliserDiscussion = true;
                    r.PeutVoirCommandes = true;
                    r.PeutGererCommandes = true;
                    // Module achats : accès = PeutGererAchats, écriture = PeutConfirmerAchats.
                    r.PeutGererAchats = true;
                    r.PeutConfirmerAchats = true;
                    r.PeutVoirTaches = true;
                    r.PeutGererTaches = true;
                    r.PeutVoirProduction = true;
                    r.PeutGererProduction = true;
                });

            _lecteur = await _factory.CreateUserAsync(
                "ctx-lecteur", "Lecteur", "Seulement", r =>
                {
                    r.PeutUtiliserDiscussion = true;
                    r.PeutVoirCommandes = true;   // accès SANS écriture
                    r.PeutGererAchats = true;     // accès achats SANS écriture (pas de PeutConfirmerAchats)
                });

            _tiers = await _factory.CreateUserAsync(
                "ctx-tiers", "Tiers", "SansModule", r => r.PeutUtiliserDiscussion = true);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ── Fabrique de données ──────────────────────────────────────────────

        private async Task<int> CreerCommandeAsync(decimal montant = 1234.5m)
        {
            var clientId = await _factory.CreateClientAsync("ctx");
            return await _factory.WithDbAsync(async ctx =>
            {
                var commande = new CommandeClient
                {
                    NumeroCommande = "CMD-CTX-" + Guid.NewGuid().ToString("N")[..8],
                    TitreCommande = "Commande contextuelle",
                    ClientId = clientId,
                    DateCommande = DateTime.Now,
                    Statut = StatutCommande.EnProduction,
                    MontantTotal = montant,
                    Devise = "TND"
                };
                ctx.CommandesClients.Add(commande);
                await ctx.SaveChangesAsync();
                return commande.Id;
            });
        }

        private async Task<int> CreerAchatAsync(decimal montant = 9999m)
        {
            var fournisseurId = await _factory.WithDbAsync(async ctx =>
            {
                var fournisseur = new Fournisseur
                {
                    NomEntreprise = "Fournisseur ctx " + Guid.NewGuid().ToString("N")[..6],
                    Email = $"f.{Guid.NewGuid():N}@ims.test"
                };
                ctx.Fournisseurs.Add(fournisseur);
                await ctx.SaveChangesAsync();
                return fournisseur.Id;
            });

            return await _factory.WithDbAsync(async ctx =>
            {
                var achat = new Achat
                {
                    NumeroAchat = "ACH-CTX-" + Guid.NewGuid().ToString("N")[..8],
                    FournisseurId = fournisseurId,
                    Statut = StatutAchat.Confirme,
                    DateAchat = DateTime.Now,
                    MontantTotal = montant,
                    Devise = "TND"
                };
                ctx.Achats.Add(achat);
                await ctx.SaveChangesAsync();
                return achat.Id;
            });
        }

        private async Task<int> CreerTacheAsync(string assignedTo, string createdBy)
        {
            return await _factory.WithDbAsync(async ctx =>
            {
                var tache = new TacheProduction
                {
                    Titre = "Tâche contextuelle",
                    AssignedToUserId = assignedTo,
                    CreatedByUserId = createdBy,
                    Statut = StatutTache.EnCours
                };
                ctx.TachesProduction.Add(tache);
                await ctx.SaveChangesAsync();
                return tache.Id;
            });
        }

        // ── Helpers HTTP ─────────────────────────────────────────────────────

        private static int IdConversation(string corpsJson) =>
            JsonDocument.Parse(corpsJson).RootElement.GetProperty("id").GetInt32();

        private async Task<int> CreerGroupeAsync(TestUser createur, IEnumerable<string> autres, string nom)
        {
            var reponse = await createur.Client.PostAsJsonAsync("/api/Discussion", new
            {
                type = "Groupe",
                nom,
                membreIds = autres
            });
            reponse.EnsureSuccessStatusCode();
            return IdConversation(await reponse.Content.ReadAsStringAsync());
        }

        private async Task<long> EnvoyerAsync(TestUser auteur, int conversationId, string contenu)
        {
            var reponse = await auteur.Client.PostAsJsonAsync(
                $"/api/Discussion/{conversationId}/messages", new { contenu });
            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
            return JsonDocument.Parse(await reponse.Content.ReadAsStringAsync())
                .RootElement.GetProperty("id").GetInt64();
        }

        private async Task<JsonElement> MessageAsync(TestUser utilisateur, int conversationId, long messageId)
        {
            var reponse = await utilisateur.Client.GetAsync($"/api/Discussion/{conversationId}/messages");
            reponse.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync());
            return doc.RootElement.EnumerateArray()
                .First(m => m.GetProperty("id").GetInt64() == messageId);
        }

        // ── Ouverture contextuelle : module requis ───────────────────────────

        [Fact]
        public async Task Ouvrir_Commande_403SansModule_Puis201EtIdempotent()
        {
            var commandeId = await CreerCommandeAsync();

            // Sans le module « commandes », l'ouverture est refusée (403 explicite).
            var refus = await _tiers.Client.PostAsync(
                $"/api/Discussion/contexte/commande/{commandeId}", null);
            Assert.Equal(HttpStatusCode.Forbidden, refus.StatusCode);

            // Avec le module, la conversation est créée.
            var creation = await _auteur.Client.PostAsync(
                $"/api/Discussion/contexte/commande/{commandeId}", null);
            Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
            var id = IdConversation(await creation.Content.ReadAsStringAsync());
            Assert.True(id > 0);

            // Idempotence : un second appel renvoie LA MÊME conversation (200).
            var seconde = await _auteur.Client.PostAsync(
                $"/api/Discussion/contexte/commande/{commandeId}", null);
            Assert.Equal(HttpStatusCode.OK, seconde.StatusCode);
            Assert.Equal(id, IdConversation(await seconde.Content.ReadAsStringAsync()));
        }

        [Fact]
        public async Task Ouvrir_SegmentInconnu_400()
        {
            var reponse = await _auteur.Client.PostAsync("/api/Discussion/contexte/inconnu/1", null);
            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        // ── Accès contextuel gouverné par le MODULE, pas par l'appartenance ──

        [Fact]
        public async Task Contexte_LecteurAvecModuleMaisNonParticipant_PeutLireEtEcrire()
        {
            var commandeId = await CreerCommandeAsync();
            var conversation = IdConversation(await (await _auteur.Client.PostAsync(
                $"/api/Discussion/contexte/commande/{commandeId}", null))
                .Content.ReadAsStringAsync());

            // Le lecteur (module commandes, EN LECTURE) n'est pas participant, mais il
            // accède quand même à la discussion : c'est le module qui décide.
            var messages = await _lecteur.Client.GetAsync($"/api/Discussion/{conversation}/messages");
            Assert.Equal(HttpStatusCode.OK, messages.StatusCode);

            // Écrire fait de lui un participant ; l'accès n'est pas restreint pour autant.
            var ecriture = await _lecteur.Client.PostAsJsonAsync(
                $"/api/Discussion/{conversation}/messages", new { contenu = "je participe" });
            Assert.Equal(HttpStatusCode.Created, ecriture.StatusCode);
        }

        [Fact]
        public async Task Contexte_ExclueDeLaListeGlobale()
        {
            var commandeId = await CreerCommandeAsync();
            var conversation = IdConversation(await (await _auteur.Client.PostAsync(
                $"/api/Discussion/contexte/commande/{commandeId}", null))
                .Content.ReadAsStringAsync());

            var liste = JsonDocument.Parse(
                await (await _auteur.Client.GetAsync("/api/Discussion")).Content.ReadAsStringAsync());
            Assert.DoesNotContain(conversation, liste.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32()));
        }

        // ── Participants : responsables assignés, résolus par FK ─────────────

        [Fact]
        public async Task Contexte_Tache_SeedeResponsableEtCreateur()
        {
            var tacheId = await CreerTacheAsync(_auteur.Id, _lecteur.Id);

            var creation = await _auteur.Client.PostAsync(
                $"/api/Discussion/contexte/tache/{tacheId}", null);
            creation.EnsureSuccessStatusCode();

            var membres = JsonDocument.Parse(await creation.Content.ReadAsStringAsync())
                .RootElement.GetProperty("membres").EnumerateArray()
                .Select(m => m.GetProperty("userId").GetString())
                .ToHashSet();

            Assert.Contains(_auteur.Id, membres);   // responsable assigné
            Assert.Contains(_lecteur.Id, membres);  // créateur
        }

        // ── Cartes : montant seulement pour le droit d'ÉCRITURE ──────────────

        [Fact]
        public async Task Reference_Commande_MontantSeulementPourDroitEcriture()
        {
            var commandeId = await CreerCommandeAsync(4321.25m);
            var groupe = await CreerGroupeAsync(
                _auteur, new[] { _lecteur.Id, _tiers.Id }, "Carte commande");
            var msgId = await EnvoyerAsync(_auteur, groupe, $"regarde #CMD-{commandeId} stp");

            // Écriture : montant, devise et client présents.
            var carteAuteur = (await MessageAsync(_auteur, groupe, msgId))
                .GetProperty("references").EnumerateArray().Single();
            Assert.Equal("CMD-" + commandeId, carteAuteur.GetProperty("reference").GetString());
            Assert.Equal(4321.25m, carteAuteur.GetProperty("montant").GetDecimal());
            Assert.Equal("TND", carteAuteur.GetProperty("devise").GetString());
            Assert.False(string.IsNullOrEmpty(carteAuteur.GetProperty("tiers").GetString()));

            // Lecture seule : la carte existe, mais AUCUN montant ni tiers.
            var carteLecteur = (await MessageAsync(_lecteur, groupe, msgId))
                .GetProperty("references").EnumerateArray().Single();
            Assert.Equal("CMD-" + commandeId, carteLecteur.GetProperty("reference").GetString());
            Assert.Equal(JsonValueKind.Null, carteLecteur.GetProperty("montant").ValueKind);
            Assert.Equal(JsonValueKind.Null, carteLecteur.GetProperty("devise").ValueKind);
            Assert.Equal(JsonValueKind.Null, carteLecteur.GetProperty("tiers").ValueKind);
        }

        [Fact]
        public async Task Reference_Achat_MontantSeulementPourDroitEcriture()
        {
            var achatId = await CreerAchatAsync(7777m);
            var groupe = await CreerGroupeAsync(
                _auteur, new[] { _lecteur.Id, _tiers.Id }, "Carte achat");
            var msgId = await EnvoyerAsync(_auteur, groupe, $"voir #ACH-{achatId}");

            var carteAuteur = (await MessageAsync(_auteur, groupe, msgId))
                .GetProperty("references").EnumerateArray().Single();
            Assert.Equal(7777m, carteAuteur.GetProperty("montant").GetDecimal());

            var carteLecteur = (await MessageAsync(_lecteur, groupe, msgId))
                .GetProperty("references").EnumerateArray().Single();
            Assert.Equal(JsonValueKind.Null, carteLecteur.GetProperty("montant").ValueKind);
        }

        [Fact]
        public async Task Reference_AbsenteSansModule()
        {
            var commandeId = await CreerCommandeAsync();
            var groupe = await CreerGroupeAsync(
                _auteur, new[] { _lecteur.Id, _tiers.Id }, "Carte masquée");
            var msgId = await EnvoyerAsync(_auteur, groupe, $"secret #CMD-{commandeId}");

            // Le tiers n'a AUCUN module métier : le jeton ne produit aucune carte.
            var carteTiers = (await MessageAsync(_tiers, groupe, msgId))
                .GetProperty("references");
            Assert.Equal(0, carteTiers.GetArrayLength());
        }

        // ── Autocomplétion : uniquement les objets accessibles ───────────────

        [Fact]
        public async Task Autocomplete_NeListeQueLesObjetsAccessibles()
        {
            var commandeId = await CreerCommandeAsync();

            var pourAuteur = JsonDocument.Parse(
                await (await _auteur.Client.GetAsync("/api/Discussion/references?q=CMD"))
                    .Content.ReadAsStringAsync());
            Assert.Contains("CMD-" + commandeId, pourAuteur.RootElement.EnumerateArray()
                .Select(r => r.GetProperty("reference").GetString()));

            var pourTiers = JsonDocument.Parse(
                await (await _tiers.Client.GetAsync("/api/Discussion/references?q=CMD"))
                    .Content.ReadAsStringAsync());
            Assert.Equal(0, pourTiers.RootElement.GetArrayLength());
        }

        // ── Réponse : notification via la cloche ─────────────────────────────

        [Fact]
        public async Task Reponse_NotifieLAuteurDuMessageDOrigine()
        {
            var groupe = await CreerGroupeAsync(_auteur, new[] { _lecteur.Id }, "Fil de réponse");
            var msgOrigine = await EnvoyerAsync(_lecteur, groupe, "question ?");

            var reponse = await _auteur.Client.PostAsJsonAsync(
                $"/api/Discussion/{groupe}/messages",
                new { contenu = "réponse !", reponseAId = msgOrigine });
            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
            var msgReponse = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync())
                .RootElement.GetProperty("id").GetInt64();

            var notification = await _factory.WithDbAsync(ctx =>
                ctx.Notifications.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.MessageDiscussionId == msgReponse));

            Assert.NotNull(notification);
            Assert.Equal(TypeNotification.ReponseDiscussion, notification!.Type);
            Assert.Equal(_lecteur.Id, notification.UtilisateurId);
        }

        // ── Conversation contextuelle : ni « quitter » ni ajout manuel ──────

        [Fact]
        public async Task Contexte_QuitterEtAjouterMembres_SontRefuses()
        {
            var commandeId = await CreerCommandeAsync();
            var conversation = IdConversation(await (await _auteur.Client.PostAsync(
                $"/api/Discussion/contexte/commande/{commandeId}", null))
                .Content.ReadAsStringAsync());

            var quitter = await _auteur.Client.DeleteAsync($"/api/Discussion/{conversation}/quitter");
            Assert.Equal(HttpStatusCode.BadRequest, quitter.StatusCode);

            var ajout = await _auteur.Client.PostAsJsonAsync(
                $"/api/Discussion/{conversation}/membres", new { membreIds = new[] { _tiers.Id } });
            Assert.Equal(HttpStatusCode.BadRequest, ajout.StatusCode);
        }
    }
}
