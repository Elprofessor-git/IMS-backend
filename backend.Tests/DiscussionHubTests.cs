using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Backend_Gestion_Magasin_API.Authentication;
using Backend_Gestion_Magasin_API.Hubs;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Tests
{
    /// <summary>
    /// Temps réel Discussion — authentification du hub par TICKET à usage unique.
    ///
    /// Ce que ces tests verrouillent, indépendamment du transport SignalR :
    /// <list type="bullet">
    /// <item>le ticket est OPAQUE, à usage unique, lié au SecurityStamp et expirant
    /// en 60 s ;</item>
    /// <item>il est émis par une route REST protégée, et REFUSÉ par toutes les routes
    /// REST (seul le hub le comprend) ;</item>
    /// <item>le hub n'expose AUCUNE méthode cliente : impossible de choisir un groupe,
    /// donc impossible de s'abonner à une conversation dont on n'est pas membre.</item>
    /// </list>
    /// </summary>
    public class DiscussionHubTests : IClassFixture<TacheApiFactory>, IAsyncLifetime
    {
        private readonly TacheApiFactory _factory;
        private TestUser _user = null!;

        public DiscussionHubTests(TacheApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _user = await _factory.CreateUserAsync(
                "hub-user", "Hub", "Reel", r => r.PeutUtiliserDiscussion = true);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        // ── Service de tickets : usage unique, expiration, liaison ───────────────

        [Fact]
        public void Ticket_usage_unique_et_liaison()
        {
            var service = new HubTicketService(NullLogger<HubTicketService>.Instance, TimeProvider.System);

            var ticket = service.Creer("u1", "stamp");

            var info = service.Consommer(ticket);
            Assert.NotNull(info);
            Assert.Equal("u1", info!.UserId);
            Assert.Equal("stamp", info.SecurityStamp);

            // Deuxième présentation : le ticket a déjà été consommé (usage unique).
            Assert.Null(service.Consommer(ticket));
        }

        [Fact]
        public void Ticket_inconnu_refuse()
        {
            var service = new HubTicketService(
                NullLogger<HubTicketService>.Instance, TimeProvider.System);
            Assert.Null(service.Consommer("jamais-emis"));
        }

        [Fact]
        public void Ticket_expire_apres_60s()
        {
            var temps = new FakeTemps(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
            var service = new HubTicketService(NullLogger<HubTicketService>.Instance, temps);

            var encore = service.Creer("u1", "stamp");
            temps.Avancer(TimeSpan.FromSeconds(59));
            Assert.NotNull(service.Consommer(encore));   // 59 s : encore valable

            var expire = service.Creer("u1", "stamp");
            temps.Avancer(TimeSpan.FromSeconds(61));
            Assert.Null(service.Consommer(expire));      // 61 s : expiré
        }

        // ── Le hub n'expose aucune méthode cliente ───────────────────────────────

        [Fact]
        public void Hub_sans_methode_cliente_ni_rejoindre_conversation()
        {
            Assert.Null(typeof(DiscussionHub).GetMethod("RejoindreConversation"));

            var callables = typeof(DiscussionHub)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .Where(n => n is not ("OnConnectedAsync" or "OnDisconnectedAsync" or "Dispose"))
                .ToList();

            Assert.Empty(callables);
        }

        [Fact]
        public void Hub_exige_le_schema_HubTicket()
        {
            var attribut = typeof(DiscussionHub).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(attribut);
            Assert.Equal(HubTicketAuthenticationHandler.SchemeName, attribut!.AuthenticationSchemes);
        }

        [Fact]
        public void Groupes_derives_par_le_serveur()
        {
            Assert.Equal("utilisateur:u-42", DiscussionHub.GroupeUtilisateur("u-42"));
            Assert.Equal("conversation:7", DiscussionHub.GroupeConversation(7));
        }

        // ── Route REST émettrice + refus des tickets par le REST ─────────────────

        [Fact]
        public async Task Hub_ticket_requiert_une_session()
        {
            using var client = _factory.CreateClient();
            var reponse = await client.PostAsync("/api/Discussion/hub-ticket", content: null);
            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        }

        [Fact]
        public async Task Hub_ticket_refuse_sans_le_module()
        {
            var sansModule = await _factory.CreateUserAsync(
                "hub-sans-module", "Sans", "Module", _ => { });

            var reponse = await sansModule.Client.PostAsync("/api/Discussion/hub-ticket", content: null);
            Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
        }

        [Fact]
        public async Task Hub_ticket_emis_par_route_protegee()
        {
            var reponse = await _user.Client.PostAsync("/api/Discussion/hub-ticket", content: null);
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<TicketReponse>();
            Assert.NotNull(corps);
            Assert.False(string.IsNullOrWhiteSpace(corps!.Ticket));
            Assert.Equal(60, corps.ExpiresInSeconds);
        }

        [Fact]
        public async Task Ticket_refuse_par_les_routes_REST_query_string()
        {
            var ticket = await ObtenirTicketAsync(_user.Client);

            using var client = _factory.CreateClient();
            var reponse = await client.GetAsync(
                $"/api/Discussion/sync?depuis=0&access_token={Uri.EscapeDataString(ticket)}");

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        }

        [Fact]
        public async Task Ticket_refuse_par_les_routes_REST_en_entete()
        {
            var ticket = await ObtenirTicketAsync(_user.Client);

            using var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ticket);

            var reponse = await client.GetAsync("/api/Discussion/sync?depuis=0");
            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        }

        // ── Handler d'authentification (sans transport SignalR) ──────────────────

        [Fact]
        public async Task Handler_accepte_un_ticket_valide()
        {
            var user = await CreerAsync("hub-valid");
            var ticket = await EmettreTicketAsync(user.Id);
            var resultat = await AuthentifierAsync(ticket);

            Assert.True(resultat.Succeeded);
            Assert.Equal(user.Id, resultat.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        [Fact]
        public async Task Handler_refuse_un_ticket_reutilise()
        {
            var user = await CreerAsync("hub-reuse");
            var ticket = await EmettreTicketAsync(user.Id);

            Assert.True((await AuthentifierAsync(ticket)).Succeeded);
            Assert.False((await AuthentifierAsync(ticket)).Succeeded);
        }

        [Fact]
        public async Task Handler_refuse_un_compte_desactive()
        {
            // Compte dédié : le désactiver ne doit pas polluer les autres tests.
            var user = await CreerAsync("hub-inactif");
            var ticket = await EmettreTicketAsync(user.Id);

            await DansScopeAsync(async sp =>
            {
                var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var u = (await um.FindByIdAsync(user.Id))!;
                u.EstActif = false;
                await um.UpdateAsync(u);
            });

            Assert.False((await AuthentifierAsync(ticket)).Succeeded);
        }

        [Fact]
        public async Task Handler_refuse_un_security_stamp_perime()
        {
            // Compte dédié : changer le stamp est irréversible pour ce compte.
            var user = await CreerAsync("hub-stamp");
            var ticket = await EmettreTicketAsync(user.Id);

            // Mot de passe / rôle changé APRÈS l'émission du ticket.
            await DansScopeAsync(async sp =>
            {
                var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await um.UpdateSecurityStampAsync((await um.FindByIdAsync(user.Id))!);
            });

            Assert.False((await AuthentifierAsync(ticket)).Succeeded);
        }

        [Fact]
        public async Task Handler_sans_ticket_ne_prend_pas_de_decision()
        {
            var resultat = await AuthentifierSansTicketAsync();
            Assert.False(resultat.Succeeded);
            Assert.Null(resultat.Failure);
            Assert.Null(resultat.Principal);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<T> DansScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
        {
            using var scope = _factory.Services.CreateScope();
            return await action(scope.ServiceProvider);
        }

        /// <summary>Compte dédié avec le module Discussion, isolé des autres tests.</summary>
        private Task<TestUser> CreerAsync(string id) =>
            _factory.CreateUserAsync(id, "Test", id, r => r.PeutUtiliserDiscussion = true);

        private async Task DansScopeAsync(Func<IServiceProvider, Task> action)
        {
            using var scope = _factory.Services.CreateScope();
            await action(scope.ServiceProvider);
        }

        private static async Task<string> ObtenirTicketAsync(HttpClient client)
        {
            var reponse = await client.PostAsync("/api/Discussion/hub-ticket", content: null);
            reponse.EnsureSuccessStatusCode();
            var corps = await reponse.Content.ReadFromJsonAsync<TicketReponse>();
            return corps!.Ticket;
        }

        /// <summary>Émet un ticket via le SERVICE, sans passer par la route REST.</summary>
        private Task<string> EmettreTicketAsync(string userId) =>
            DansScopeAsync(async sp =>
            {
                var service = sp.GetRequiredService<IHubTicketService>();
                var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var stamp = (await um.FindByIdAsync(userId))!.SecurityStamp;
                return service.Creer(userId, stamp);
            });

        private static AuthenticationScheme Schema() => new(
            HubTicketAuthenticationHandler.SchemeName, null,
            typeof(HubTicketAuthenticationHandler));

        /// <summary>Exécute le handler sur un HttpContext portant <c>?access_token=…</c>.</summary>
        private Task<AuthenticateResult> AuthentifierAsync(string ticket) =>
            DansScopeAsync(async sp =>
            {
                var handler = ConstruireHandler(sp);
                var http = new DefaultHttpContext();
                http.Request.QueryString = new QueryString($"?access_token={Uri.EscapeDataString(ticket)}");

                await handler.InitializeAsync(Schema(), http);
                return await handler.AuthenticateAsync();
            });

        /// <summary>Exécute le handler SANS aucun ticket (décision « aucune »).</summary>
        private Task<AuthenticateResult> AuthentifierSansTicketAsync() =>
            DansScopeAsync(async sp =>
            {
                var handler = ConstruireHandler(sp);
                var http = new DefaultHttpContext();

                await handler.InitializeAsync(Schema(), http);
                return await handler.AuthenticateAsync();
            });

        private static HubTicketAuthenticationHandler ConstruireHandler(IServiceProvider sp)
        {
            var options = sp.GetRequiredService<IOptionsMonitor<HubTicketAuthenticationOptions>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var service = sp.GetRequiredService<IHubTicketService>();
            var um = sp.GetRequiredService<UserManager<ApplicationUser>>();

            return new HubTicketAuthenticationHandler(
                options, loggerFactory, UrlEncoder.Default, service, um);
        }

        private sealed record TicketReponse(string Ticket, int ExpiresInSeconds);

        private sealed class FakeTemps : TimeProvider
        {
            private DateTimeOffset _now;

            public FakeTemps(DateTimeOffset now) => _now = now;

            public override DateTimeOffset GetUtcNow() => _now;

            public void Avancer(TimeSpan duree) => _now += duree;
        }
    }
}
