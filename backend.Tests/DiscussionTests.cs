using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    /// <summary>
    /// Module Discussion (E1). Alice, Bob et Carol partagent la fixture (une base
    /// jetable par classe) ; les tests destructifs (compte désactivé, compte externe,
    /// limite de débit) utilisent des utilisateurs DÉDIÉS pour ne pas contaminer le
    /// reste de la classe.
    /// </summary>
    public class DiscussionTests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;
        private TestUser _alice = null!;
        private TestUser _bob = null!;
        private TestUser _carol = null!;

        public DiscussionTests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _alice = await _factory.CreateUserAsync(
                "disc-alice", "Alice", "Alpha", r => r.PeutUtiliserDiscussion = true);
            _bob = await _factory.CreateUserAsync(
                "disc-bob", "Bob", "Beta", r => r.PeutUtiliserDiscussion = true);
            _carol = await _factory.CreateUserAsync(
                "disc-carol", "Carol", "Gamma", r => r.PeutUtiliserDiscussion = true);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<int> CreerDmAsync(TestUser auteur, string autreId)
        {
            var reponse = await auteur.Client.PostAsJsonAsync("/api/Discussion", new
            {
                type = "Direct",
                membreIds = new[] { autreId }
            });
            reponse.EnsureSuccessStatusCode();
            return IdConversation(await reponse.Content.ReadAsStringAsync());
        }

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
            var json = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync()).RootElement;
            return json.GetProperty("id").GetInt64();
        }

        private static int IdConversation(string corpsJson) =>
            JsonDocument.Parse(corpsJson).RootElement.GetProperty("id").GetInt32();

        private async Task<int> NonLusAsync(TestUser utilisateur, int conversationId)
        {
            var reponse = await utilisateur.Client.GetAsync("/api/Discussion");
            reponse.EnsureSuccessStatusCode();
            var json = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync()).RootElement;
            foreach (var conversation in json.EnumerateArray())
                if (conversation.GetProperty("id").GetInt32() == conversationId)
                    return conversation.GetProperty("nonLus").GetInt32();
            throw new InvalidOperationException("La conversation est absente de la liste de l'utilisateur.");
        }

        // ── DM unique, même sous requêtes simultanées ───────────────────────

        [Fact]
        public async Task Creer_DmSimultane_UneSeuleConversation()
        {
            var requetes = new[]
            {
                _alice.Client.PostAsJsonAsync("/api/Discussion", new { type = "Direct", membreIds = new[] { _bob.Id } }),
                _bob.Client.PostAsJsonAsync("/api/Discussion", new { type = "Direct", membreIds = new[] { _alice.Id } })
            };

            var reponses = await Task.WhenAll(requetes);
            var ids = new List<int>();
            foreach (var reponse in reponses)
            {
                reponse.EnsureSuccessStatusCode();
                ids.Add(IdConversation(await reponse.Content.ReadAsStringAsync()));
            }

            // Un seul DM pour la paire, quel que soit le gagnant de la course.
            Assert.Equal(2, ids.Count);
            Assert.Equal(ids[0], ids[1]);

            var cle = _factory.WithDbAsync(async ctx =>
            {
                var cleReelle = string.CompareOrdinal(_alice.Id, _bob.Id) < 0
                    ? _alice.Id + ":" + _bob.Id
                    : _bob.Id + ":" + _alice.Id;
                return await ctx.Conversations.CountAsync(c => c.CleDirecte == cleReelle);
            });
            Assert.Equal(1, await cle);
        }

        // ── Non-membre : 404 partout, jamais 403 ────────────────────────────

        [Fact]
        public async Task NonMembre_404_SurListeLectureEnvoiMarquageEtMessages()
        {
            var dm = await CreerDmAsync(_alice, _bob.Id);
            var msgId = await EnvoyerAsync(_bob, dm, "salut");

            // La conversation n'apparaît pas dans la liste de Carol.
            var liste = JsonDocument.Parse(
                await (await _carol.Client.GetAsync("/api/Discussion")).Content.ReadAsStringAsync());
            Assert.DoesNotContain(dm, liste.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32()));

            Assert.Equal(HttpStatusCode.NotFound,
                (await _carol.Client.GetAsync($"/api/Discussion/{dm}/messages")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await _carol.Client.PostAsJsonAsync($"/api/Discussion/{dm}/messages", new { contenu = "intrusion" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await _carol.Client.PostAsJsonAsync($"/api/Discussion/{dm}/lire", new { jusquaMessageId = msgId })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await _carol.Client.PutAsJsonAsync($"/api/Discussion/{dm}/messages/{msgId}", new { contenu = "modif" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await _carol.Client.DeleteAsync($"/api/Discussion/{dm}/messages/{msgId}")).StatusCode);
        }

        // ── Non-lus calculés en SQL ─────────────────────────────────────────

        [Fact]
        public async Task NonLus_CalculesEnSql_EtMarquageNeReculePas()
        {
            // Groupe DÉDIÉ : la déduplication des DM partagerait la conversation
            // alice-bob avec les autres tests de la classe.
            var groupe = await CreerGroupeAsync(_bob, new[] { _alice.Id }, "Groupe non-lus");

            await EnvoyerAsync(_bob, groupe, "premier");
            var m2 = await EnvoyerAsync(_bob, groupe, "deuxieme");
            await EnvoyerAsync(_bob, groupe, "troisieme");

            Assert.Equal(3, await NonLusAsync(_alice, groupe));

            // Marquer lu jusqu'à m2 : il reste un seul non-lu.
            var lu = await _alice.Client.PostAsJsonAsync(
                $"/api/Discussion/{groupe}/lire", new { jusquaMessageId = m2 });
            Assert.Equal(HttpStatusCode.NoContent, lu.StatusCode);
            Assert.Equal(1, await NonLusAsync(_alice, groupe));

            // Lire « au-delà » du dernier : tout est lu, le pointeur ne recule pas.
            var trop = await _alice.Client.PostAsJsonAsync(
                $"/api/Discussion/{groupe}/lire", new { jusquaMessageId = 999_999_999L });
            Assert.Equal(HttpStatusCode.NoContent, trop.StatusCode);
            Assert.Equal(0, await NonLusAsync(_alice, groupe));

            // Propre réponse d'Alice : non-lu pour Bob (auteur différent), jamais pour elle.
            await EnvoyerAsync(_alice, groupe, "reponse");
            Assert.Equal(1, await NonLusAsync(_bob, groupe));
            Assert.Equal(0, await NonLusAsync(_alice, groupe));
        }

        // ── Annuaire : actifs et internes, sans moi ─────────────────────────

        [Fact]
        public async Task Annuaire_ActifsEtInternes_SansMoi()
        {
            var annuaire = JsonDocument.Parse(
                await (await _alice.Client.GetAsync("/api/Discussion/utilisateurs")).Content.ReadAsStringAsync());

            var ids = annuaire.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("userId").GetString())
                .ToHashSet();

            Assert.Contains(_bob.Id, ids);
            Assert.Contains(_carol.Id, ids);
            Assert.DoesNotContain(_alice.Id, ids); // jamais soi-même
        }

        // ── Compte désactivé : absent de l'annuaire et refusé (401) ─────────

        [Fact]
        public async Task UtilisateurDesactive_AbsentDeLAnnuaire_EtRefuse()
        {
            var desactive = await _factory.CreateUserAsync(
                "disc-desactivee", "Des", "Activee", r => r.PeutUtiliserDiscussion = true);

            // Annuaire : présent tant qu'il est actif, disparaît après désactivation.
            var avant = JsonDocument.Parse(
                await (await _alice.Client.GetAsync("/api/Discussion/utilisateurs")).Content.ReadAsStringAsync());
            Assert.Contains(desactive.Id, avant.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("userId").GetString()));

            await _factory.WithDbAsync(async ctx =>
            {
                var user = await ctx.Users.FirstAsync(u => u.Id == desactive.Id);
                user.EstActif = false;
                await ctx.SaveChangesAsync();
            });

            var apres = JsonDocument.Parse(
                await (await _alice.Client.GetAsync("/api/Discussion/utilisateurs")).Content.ReadAsStringAsync());
            Assert.DoesNotContain(desactive.Id, apres.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("userId").GetString()));

            // Le jeton du compte désactivé est révoqué dès la prochaine requête.
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await desactive.Client.GetAsync("/api/Discussion")).StatusCode);

            // Impossible d'ouvrir une discussion vers un compte désactivé.
            var refus = await _alice.Client.PostAsJsonAsync("/api/Discussion", new
            {
                type = "Direct",
                membreIds = new[] { desactive.Id }
            });
            Assert.Equal(HttpStatusCode.BadRequest, refus.StatusCode);
        }

        // ── Compte externe : refusé partout, absent de l'annuaire ───────────

        [Fact]
        public async Task CompteExterne_Refuse_EtAbsentDeLAnnuaire()
        {
            var externe = await _factory.CreateUserAsync(
                "disc-externe", "Externe", "Portail", r => r.PeutUtiliserDiscussion = true);
            await _factory.SetCompteInterneAsync(externe.Id, interne: false);

            // RequireCompteInterne (filtre de classe) : refus 403, même avec le drapeau.
            Assert.Equal(HttpStatusCode.Forbidden,
                (await externe.Client.GetAsync("/api/Discussion")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await externe.Client.GetAsync("/api/Discussion/utilisateurs")).StatusCode);

            // Jamais proposé comme destinataire.
            var annuaire = JsonDocument.Parse(
                await (await _alice.Client.GetAsync("/api/Discussion/utilisateurs")).Content.ReadAsStringAsync());
            Assert.DoesNotContain(externe.Id, annuaire.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("userId").GetString()));

            var refus = await _alice.Client.PostAsJsonAsync("/api/Discussion", new
            {
                type = "Direct",
                membreIds = new[] { externe.Id }
            });
            Assert.Equal(HttpStatusCode.BadRequest, refus.StatusCode);
        }

        // ── Drapeau absent : 403 ────────────────────────────────────────────

        [Fact]
        public async Task DrapeauAbsent_403_SurToutLeModule()
        {
            var sansDroit = await _factory.CreateUserAsync("disc-sansdroit", "Sans", "Droit");

            Assert.Equal(HttpStatusCode.Forbidden,
                (await sansDroit.Client.GetAsync("/api/Discussion")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await sansDroit.Client.GetAsync("/api/Discussion/utilisateurs")).StatusCode);
        }

        // ── Mentions : notification via le module Notification ─────────────

        [Fact]
        public async Task Mention_CreeUneNotification_PourLeDestinataire()
        {
            var groupe = await CreerGroupeAsync(_bob, new[] { _alice.Id }, "Groupe mention");
            var msgId = await EnvoyerAsync(_bob, groupe, "salut @Alice, le rapport est prêt");

            var notification = await _factory.WithDbAsync(ctx =>
                ctx.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.MessageDiscussionId == msgId));

            Assert.NotNull(notification);
            Assert.Equal(TypeNotification.MentionDiscussion, notification!.Type);
            Assert.Equal(_alice.Id, notification.UtilisateurId);

            // Pas de notification vers l'auteur (lui-même), ni vers un non-membre.
            var nb = await _factory.WithDbAsync(ctx =>
                ctx.Notifications.CountAsync(n => n.MessageDiscussionId == msgId));
            Assert.Equal(1, nb);
        }

        // ── Modification / suppression : MES messages seulement, soft delete ─

        [Fact]
        public async Task Message_ModificationEtSuppression_PropresMessagesSeulement()
        {
            var groupe = await CreerGroupeAsync(_bob, new[] { _alice.Id }, "Groupe modif");
            var msgId = await EnvoyerAsync(_bob, groupe, "version 1");

            // Alice ne peut ni modifier ni supprimer le message de Bob.
            Assert.Equal(HttpStatusCode.Forbidden, (await _alice.Client.PutAsJsonAsync(
                $"/api/Discussion/{groupe}/messages/{msgId}", new { contenu = "piratage" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _alice.Client.DeleteAsync(
                $"/api/Discussion/{groupe}/messages/{msgId}")).StatusCode);

            // Bob modifie son propre message.
            Assert.Equal(HttpStatusCode.OK, (await _bob.Client.PutAsJsonAsync(
                $"/api/Discussion/{groupe}/messages/{msgId}", new { contenu = "version 2" })).StatusCode);

            // Bob supprime : soft delete (ligne conservée, contenu vidé).
            Assert.Equal(HttpStatusCode.NoContent, (await _bob.Client.DeleteAsync(
                $"/api/Discussion/{groupe}/messages/{msgId}")).StatusCode);

            var historique = JsonDocument.Parse(await (await _alice.Client.GetAsync(
                $"/api/Discussion/{groupe}/messages")).Content.ReadAsStringAsync());
            var msg = historique.RootElement.EnumerateArray()
                .First(m => m.GetProperty("id").GetInt64() == msgId);
            Assert.True(msg.GetProperty("estSupprime").GetBoolean());
            Assert.Equal(string.Empty, msg.GetProperty("contenu").GetString());
        }

        // ── Sync : la revision est renouvelée à chaque modification ─────────

        [Fact]
        public async Task Sync_PropageModificationEtSuppression_ParCurseur()
        {
            var groupe = await CreerGroupeAsync(_bob, new[] { _alice.Id }, "Groupe sync");
            var msgId = await EnvoyerAsync(_bob, groupe, "sync 1");

            var sync0 = await SyncAsync(_alice, 0);
            var msg0 = sync0.RootElement.GetProperty("messages").EnumerateArray()
                .First(m => m.GetProperty("id").GetInt64() == msgId);
            Assert.Equal("sync 1", msg0.GetProperty("contenu").GetString());
            var curseur0 = sync0.RootElement.GetProperty("curseur").GetInt64();

            await _bob.Client.PutAsJsonAsync($"/api/Discussion/{groupe}/messages/{msgId}", new { contenu = "sync 2" });

            // La modification a renouvelé la revision : visible au curseur suivant.
            var sync1 = await SyncAsync(_alice, curseur0);
            var msg1 = sync1.RootElement.GetProperty("messages").EnumerateArray()
                .First(m => m.GetProperty("id").GetInt64() == msgId);
            Assert.Equal("sync 2", msg1.GetProperty("contenu").GetString());
            Assert.False(msg1.GetProperty("estSupprime").GetBoolean());
            var curseur1 = sync1.RootElement.GetProperty("curseur").GetInt64();

            await _bob.Client.DeleteAsync($"/api/Discussion/{groupe}/messages/{msgId}");

            // La suppression est également propagée (contenu vide + drapeau).
            var sync2 = await SyncAsync(_alice, curseur1);
            var msg2 = sync2.RootElement.GetProperty("messages").EnumerateArray()
                .First(m => m.GetProperty("id").GetInt64() == msgId);
            Assert.True(msg2.GetProperty("estSupprime").GetBoolean());
            Assert.Equal(string.Empty, msg2.GetProperty("contenu").GetString());
        }

        private async Task<JsonDocument> SyncAsync(TestUser utilisateur, long depuis)
        {
            var reponse = await utilisateur.Client.GetAsync($"/api/Discussion/sync?depuis={depuis}");
            reponse.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await reponse.Content.ReadAsStringAsync());
        }

        // ── Limite de débit : 30/min par utilisateur ────────────────────────

        [Fact]
        public async Task Envoyer_RateLimit_30ParMinute_Puis429()
        {
            var rateur = await _factory.CreateUserAsync(
                "disc-rateur", "Rateur", "Limite", r => r.PeutUtiliserDiscussion = true);
            var groupe = await CreerGroupeAsync(rateur, new[] { _alice.Id }, "Groupe debit");

            for (var i = 0; i < 30; i++)
            {
                var reponse = await rateur.Client.PostAsJsonAsync(
                    $"/api/Discussion/{groupe}/messages", new { contenu = $"message {i}" });
                Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
            }

            // Le 31e message de la minute est bloqué.
            var bloque = await rateur.Client.PostAsJsonAsync(
                $"/api/Discussion/{groupe}/messages", new { contenu = "trente-et-unieme" });
            Assert.Equal(HttpStatusCode.TooManyRequests, bloque.StatusCode);

            // La limite est PAR UTILISATEUR : Alice, distincte, écrit sans encombre.
            Assert.Equal(HttpStatusCode.Created, (await _alice.Client.PostAsJsonAsync(
                $"/api/Discussion/{groupe}/messages", new { contenu = "pour moi ca passe" })).StatusCode);
        }

        // ── Anti-régression du flot des drapeaux de rôle (LOT18 motif §1) ─────

        [Fact]
        public async Task PermissionMe_Expose_TousLesDrapeauxDiscussion()
        {
            var admin = await _factory.CreateUserAsync(
                "disc-admin-perms", "Admin", "Perms", r =>
                {
                    r.EstAdministrateur = true;
                    r.PeutUtiliserDiscussion = true;
                    r.PeutGererDiscussion = true;
                });

            var reponse = await admin.Client.GetAsync("/api/Permission/me");
            reponse.EnsureSuccessStatusCode();
            var json = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync()).RootElement;

            // Capacités transversales répétées sur CHAQUE entrée (forme du tableau inchangée).
            Assert.True(json.EnumerateArray().All(e => e.GetProperty("peutUtiliserDiscussion").GetBoolean()));
            Assert.True(json.EnumerateArray().All(e => e.GetProperty("peutGererDiscussion").GetBoolean()));
        }

        [Fact]
        public async Task Role_GetPuisPut_NeRevoquePasLesDrapeauxDiscussion()
        {
            // PUT /roles est un remplacement complet : un allers-retours GET → PUT ne
            // doit pas révoquer silencieusement les deux nouveaux drapeaux.
            var admin = await _factory.CreateUserAsync(
                "disc-admin-role", "Admin", "Role", r => r.EstAdministrateur = true);

            var creation = await admin.Client.PostAsJsonAsync("/api/roles", new
            {
                name = "RoleDisc_" + Guid.NewGuid().ToString("N")[..6],
                peutUtiliserDiscussion = true,
                peutGererDiscussion = true,
            });
            creation.EnsureSuccessStatusCode();
            var cree = JsonDocument.Parse(await creation.Content.ReadAsStringAsync()).RootElement;
            var roleId = cree.GetProperty("id").GetInt32();
            Assert.True(cree.GetProperty("peutUtiliserDiscussion").GetBoolean());
            Assert.True(cree.GetProperty("peutGererDiscussion").GetBoolean());

            var get = await admin.Client.GetAsync($"/api/roles/{roleId}");
            get.EnsureSuccessStatusCode();
            var corps = await get.Content.ReadAsStringAsync();
            Assert.True(JsonDocument.Parse(corps).RootElement.GetProperty("peutUtiliserDiscussion").GetBoolean());
            Assert.True(JsonDocument.Parse(corps).RootElement.GetProperty("peutGererDiscussion").GetBoolean());

            var put = await admin.Client.PutAsJsonAsync($"/api/roles/{roleId}", JsonDocument.Parse(corps).RootElement);
            Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

            var relu = JsonDocument.Parse(
                await (await admin.Client.GetAsync($"/api/roles/{roleId}")).Content.ReadAsStringAsync());
            Assert.True(relu.RootElement.GetProperty("peutUtiliserDiscussion").GetBoolean());
            Assert.True(relu.RootElement.GetProperty("peutGererDiscussion").GetBoolean());
        }

        // ── Canal public : modération via PeutGererDiscussion ───────────────

        [Fact]
        public async Task CanalPublic_ExigePeutGererDiscussion_AjouterMembres()
        {
            // Trois rôles distincts : le créateur (toujours admin), un membre NON-admin
            // doté de PeutGererDiscussion, et un simple membre sans ce droit.
            var createur = await _factory.CreateUserAsync(
                "disc-admin-canal", "Createur", "Canal", r =>
                {
                    r.PeutUtiliserDiscussion = true;
                    r.PeutGererDiscussion = true;
                });
            var gestionnaire = await _factory.CreateUserAsync(
                "disc-gestionnaire", "Gestion", "Naire", r =>
                {
                    r.PeutUtiliserDiscussion = true;
                    r.PeutGererDiscussion = true;
                });
            var simple = await _factory.CreateUserAsync(
                "disc-simple", "Sim", "Ple", r => r.PeutUtiliserDiscussion = true);

            // La création d'un canal public est réservée à PeutGererDiscussion.
            var sansDroit = await simple.Client.PostAsJsonAsync("/api/Discussion", new
            {
                type = "Canal",
                nom = "general",
                estPrive = false,
                membreIds = new[] { gestionnaire.Id }
            });
            Assert.Equal(HttpStatusCode.Forbidden, sansDroit.StatusCode);

            // Avec le droit, le canal public se crée ; le créateur est admin, les autres
            // n'ont que le droit d'usage.
            var avecDroit = await createur.Client.PostAsJsonAsync("/api/Discussion", new
            {
                type = "Canal",
                nom = "general",
                estPrive = false,
                membreIds = new[] { gestionnaire.Id, simple.Id }
            });
            avecDroit.EnsureSuccessStatusCode();
            var canalId = IdConversation(await avecDroit.Content.ReadAsStringAsync());

            // Le gestionnaire est membre NON-admin : seul PeutGererDiscussion (modération
            // d'un canal public) l'autorise à ajouter Carol.
            var ajout = await gestionnaire.Client.PostAsJsonAsync(
                $"/api/Discussion/{canalId}/membres", new { membreIds = new[] { _carol.Id } });
            Assert.Equal(HttpStatusCode.OK, ajout.StatusCode);

            // Un simple membre du canal n'a pas ce droit d'ajout.
            var refus = await simple.Client.PostAsJsonAsync(
                $"/api/Discussion/{canalId}/membres", new { membreIds = new[] { _carol.Id } });
            Assert.Equal(HttpStatusCode.Forbidden, refus.StatusCode);
        }
    }
}