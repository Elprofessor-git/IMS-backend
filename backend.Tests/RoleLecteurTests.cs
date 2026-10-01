using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests
{
    /// <summary>
    /// LOT « Nettoyage » — rôle « Lecteur » semé par défaut.
    ///
    /// Vérifie la double propriété : un utilisateur porteur de ce rôle LIT chaque
    /// module exposé, et n'obtient l'écriture sur AUCUN (l'API doit refuser un POST
    /// sur un module sans droit d'écriture).
    /// </summary>
    public class RoleLecteurTests : IClassFixture<AuthApiFactory>
    {
        private readonly AuthApiFactory _factory;

        public RoleLecteurTests(AuthApiFactory factory) => _factory = factory;

        private static readonly string[] ModulesLecture =
        [
            "articles", "stock", "mouvements", "achats", "importations", "commandes",
            "clients", "fournisseurs", "plateformes", "taches", "utilisateurs", "roles",
            "chatbot", "dashboard", "rapports", "factures", "machines", "coupe",
            "planning", "production", "qualite", "courriels"
        ];

        [Fact]
        public async Task Lecteur_lit_chaque_module_et_ne_peut_pas_ecrire()
        {
            var role = await _factory.WithDbAsync(ctx =>
                ctx.AppRoles.AsNoTracking().FirstAsync(r => r.NomRole == "Lecteur"));

            using var client = await _factory.CreateAuthenticatedClientAsync(
                $"lecteur.{Guid.NewGuid():N}@ims.test", roleId: role.Id);

            var reponse = await client.GetAsync("/api/Permission/me");
            reponse.EnsureSuccessStatusCode();

            var modules = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync())
                .RootElement.EnumerateArray()
                .ToDictionary(
                    e => e.GetProperty("module").GetString()!,
                    e => e);

            foreach (var (module, entree) in modules)
                Assert.False(entree.GetProperty("canWrite").GetBoolean(),
                    $"le rôle Lecteur ne doit pas écrire sur « {module} »");

            foreach (var module in ModulesLecture)
            {
                Assert.True(modules.ContainsKey(module), $"module absent : {module}");
                Assert.True(modules[module].GetProperty("canAccess").GetBoolean(),
                    $"le rôle Lecteur doit lire « {module} »");
            }

            // Écriture refusée : création de tâche (module « taches » sans PeutGererTaches).
            var ecriture = await client.PostAsJsonAsync(
                "/api/TacheProduction", new { titre = "Tâche interdite" });
            Assert.Equal(HttpStatusCode.Forbidden, ecriture.StatusCode);
        }
    }
}
