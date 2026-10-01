using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Models;

namespace Backend.Tests
{
    /// <summary>
    /// Fabrique dédiée : le seuil est fixé dans le CONSTRUCTEUR de la fabrique, car
    /// xUnit initialise la fixture (et donc construit l'hôte) AVANT de construire la
    /// classe de tests. Le fixer côté test serait trop tard.
    /// </summary>
    public sealed class PartageRateLimitFactory : TacheApiFactory
    {
        public PartageRateLimitFactory() => RateLimitParMinute = 2;
    }

    /// <summary>
    /// Limiteur du point public. Classe SÉPARÉE : elle abaisse le seuil à 2/min, ce
    /// qui ne doit pas contaminer les autres tests qui, eux, ouvrent plusieurs liens.
    /// </summary>
    public class PartageRateLimitTests : IClassFixture<PartageRateLimitFactory>, IAsyncLifetime
    {
        private readonly PartageRateLimitFactory _factory;
        private TestUser _admin = null!;

        public PartageRateLimitTests(PartageRateLimitFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            _admin = await _factory.CreateUserAsync(
                "partage-rate-admin", "Admin", "Rate",
                r => r.EstAdministrateur = true);
        }

        public async Task DisposeAsync() { }

        private async Task<string> CreerLienIllimiteAsync()
        {
            var (plateformeId, clientId, commandeId) = await _factory.WithDbAsync(async ctx =>
            {
                var p = new Plateforme { Nom = "P-rate", EstActif = true };
                ctx.Plateformes.Add(p);
                await ctx.SaveChangesAsync();
                var c = new Client
                {
                    Nom = "C-rate",
                    Email = $"rate.{Guid.NewGuid():N}@ims.test",
                    PlateformeId = p.Id,
                    EstActif = true
                };
                ctx.Clients.Add(c);
                await ctx.SaveChangesAsync();
                var k = new CommandeClient { NumeroCommande = "CMD-rate", ClientId = c.Id };
                ctx.CommandesClients.Add(k);
                await ctx.SaveChangesAsync();
                return (p.Id, c.Id, k.Id);
            });
            _ = plateformeId;
            _ = clientId;

            var creation = await _admin.Client.PostAsJsonAsync("/api/Partage", new
            {
                scopeType = 3, scopeId = commandeId, sections = 1, dureeHeures = 1
            });
            creation.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await creation.Content.ReadAsStringAsync())
                .RootElement.GetProperty("token").GetString()!;
        }

        private async Task<HttpResponseMessage> OuvrirAsync(string token, string ip)
        {
            var requete = new HttpRequestMessage(HttpMethod.Post, "/api/Partage/Public")
            {
                Content = JsonContent.Create(new { token })
            };
            requete.Headers.Add("X-Forwarded-For", ip);
            return await _factory.CreateClient().SendAsync(requete);
        }

        [Fact]
        public async Task Public_CompteLesRequetesParIp_Reelle()
        {
            var token = await CreerLienIllimiteAsync();

            // IP A : 2 autorisées, la 3e est rejetée par le limiteur (429) — et non par
            // le service (qui renverrait 200, le lien étant illimité).
            Assert.Equal(HttpStatusCode.OK, (await OuvrirAsync(token, "203.0.113.10")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await OuvrirAsync(token, "203.0.113.10")).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await OuvrirAsync(token, "203.0.113.10")).StatusCode);

            // IP B : sa propre partition, intacte malgré l'épuisement de l'IP A.
            Assert.Equal(HttpStatusCode.OK, (await OuvrirAsync(token, "203.0.113.11")).StatusCode);
        }
    }
}