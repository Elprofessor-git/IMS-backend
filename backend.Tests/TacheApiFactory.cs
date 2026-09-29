using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Backend.Tests
{
    /// <summary>
    /// Base PostgreSQL éphémère, dédiée à une classe de tests.
    /// Le schéma est obtenu par les MIGRATIONS réelles (pas EnsureCreated) : on teste donc
    /// exactement le modèle deployed, contraintes de clé étrangère et index compris.
    /// </summary>
    public sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _databaseName;
        private readonly string _adminConnectionString;

        public string ConnectionString { get; }

        public TestDatabase()
        {
            var host = Environment.GetEnvironmentVariable("IMS_TEST_PG")
                       ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=Frigoriste3.";

            _databaseName = "ims_test_" + Guid.NewGuid().ToString("N")[..12];
            _adminConnectionString = host;

            var builder = new NpgsqlConnectionStringBuilder(host) { Database = "postgres" };
            using (var conn = new NpgsqlConnection(builder.ConnectionString))
            {
                conn.Open();
                using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", conn);
                cmd.ExecuteNonQuery();
            }

            var target = new NpgsqlConnectionStringBuilder(host)
            {
                Database = _databaseName,
                // Détail des violations de contrainte : indispensable pour diagnostiquer
                // un échec de test (clé en conflit, valeur fautive).
                IncludeErrorDetail = true
            };
            ConnectionString = target.ConnectionString;
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = "postgres" };
            using var conn = new NpgsqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Fabrique d'hôte de test : démarre l'API réelle sur une base jetable.
    /// L'authentification est RÉELLEMENT exercée : les clients HTTP reçoivent un JWT
    /// valide produit par le TokenService de l'application, de sorte que [Authorize],
    /// RequireModulePermission et la lecture des claims passent par le vrai pipeline.
    /// </summary>
    public class TacheApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string TestJwtSecret = "ims_test_secret_task_ownership_0123456789";

        /// <summary>
        /// Program.cs et TokenService lisent tous deux JWT_SECRET en priorité, et le
        /// premier s'exécute avant que la configuration de la fabrique ne soit appliquée.
        /// On pose donc la variable d'environnement du processus de test pour que la clé
        /// de signature soit identique des deux côtés.
        ///
        /// Même raisonnement pour l'audience : avec l'hôte minimal, une source en mémoire
        /// ajoutée par la fabrique n'est visible que par IConfiguration résolu via DI, pas
        /// par les lectures de builder.Configuration faites au démarrage de Program.cs.
        /// Les variables d'environnement, elles, sont vues par les deux, ce qui évite que
        /// le jeton soit émis pour une audience et validé contre une autre (401).
        /// </summary>
        static TacheApiFactory()
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", TestJwtSecret);
            Environment.SetEnvironmentVariable("JwtSettings__Issuer", "Backend_Gestion_Magasin_API");
            Environment.SetEnvironmentVariable("JwtSettings__Audience", "Backend_Gestion_Magasin_API_Users");
        }

        private readonly TestDatabase _database = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _database.ConnectionString,
                    ["JwtSettings:Secret"] = TacheApiFactory.TestJwtSecret,
                });
            });

            // Le démarrage de l'application (Program.cs) applique lui-même MigrateAsync
            // et SeedData : le schéma de test est donc bien celui des migrations.
            //
            // On ré-enregistre explicitement le DbContext sur la base jetable. En effet,
            // avec l'hôte minimal (WebApplication.CreateBuilder), la surcharge de
            // configuration n'est pas toujours reprise pour la chaîne de connexion déjà
            // résolue dans Program.cs : sans cela, les tests s'exécuteraient sur la base
            // de DÉVELOPPEMENT et se contamineraient entre eux.
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(_database.ConnectionString,
                        npgsql => npgsql.EnableRetryOnFailure()));
            });
        }

        public async Task InitializeAsync()
        {
            // Force la construction de l'hôte (démarre le pipeline et applique les migrations).
            using var probe = CreateClient();

            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Garde-fou : si la surcharge de configuration n'était pas prise en compte,
            // les tests s'exécuteraient sur la base de développement et se contamineraient
            // entre eux (et avec les données locales). On échoue explicitement.
            var baseUtilisee = context.Database.GetDbConnection().Database;
            if (baseUtilisee != new NpgsqlConnectionStringBuilder(_database.ConnectionString).Database)
                throw new InvalidOperationException(
                    $"L'hôte de test pointe sur la base '{baseUtilisee}' au lieu de la base jetable.");

            await context.Database.MigrateAsync();
        }

        public new async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await _database.DisposeAsync();
        }

        // ── Fabrique de clients authentifiés ───────────────────────────────

        /// <summary>
        /// Crée un utilisateur IMS avec le rôle décrit, puis renvoie un client HTTP
        /// porteur d'un JWT réellement signé pour cet utilisateur.
        /// </summary>
        /// <summary>
        /// Nom du rôle dédié à un utilisateur de test. Un rôle par utilisateur (et non un
        /// rôle partagé) est indispensable : sinon accorder un droit global à un seul
        /// utilisateur contaminerait tous les autres tests qui partagent ce rôle.
        /// </summary>
        private static string RoleTestDe(string userId) => "TestRole_" + userId;

        public async Task<TestUser> CreateUserAsync(
            string id,
            string nom,
            string prenom,
            Action<Role>? configureRole = null)
        {
            using (var scope = Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();

                var role = await context.AppRoles.FirstOrDefaultAsync(r => r.NomRole == RoleTestDe(id));
                if (role == null)
                {
                    role = new Role
                    {
                        NomRole = RoleTestDe(id),
                        Description = "Rôle de test dédié",
                        EstActif = true
                    };
                    context.AppRoles.Add(role);
                    await context.SaveChangesAsync();
                }

                configureRole?.Invoke(role);
                await context.SaveChangesAsync();

                // Idempotent : un [Theory] rejoue ce corps pour chaque cas, sur la même
                // base, donc la création doit être sûre à répéter.
                var user = await context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        Id = id,
                        UserName = id,
                        Email = id + "@ims.test",
                        EstActif = true,
                        EmailConfirmed = true
                    };
                    context.Users.Add(user);
                }

                user.Nom = nom;
                user.Prenom = prenom;
                user.RoleId = role.Id;
                user.Role = role;

                await context.SaveChangesAsync();

                var token = tokenService.CreateToken(user);
                return new TestUser(id, nom, prenom, CreateAuthenticatedClient(token));
            }
        }

        // ── Données de référence (contraintes de clé étrangère) ────────────
        //
        // Les scopes de Stock (ClientId, CommandeClientId, GroupeCommandeId,
        // PlateformeId) et CommandeClient.ClientId sont des clés étrangères
        // SANS valeur par défaut ni cascade. Un test qui renseigne « 1 » en
        // dur suppose une ligne seedée qui n'existe pas sur la base jetable
        // (créée par les MIGRATIONS seules) : l'insertion échoue alors en
        // 23503. Ces helpers créent réellement les lignes référencées.

        /// <summary>Plateforme de référence (requis par <see cref="Client.PlateformeId"/>).</summary>
        public async Task<int> CreatePlateformeAsync(string suffixe = "")
        {
            return await WithDbAsync(async context =>
            {
                var plateforme = new Plateforme
                {
                    Nom = "Plateforme test " + suffixe + Guid.NewGuid().ToString("N")[..6],
                    EstActif = true
                };
                context.Plateformes.Add(plateforme);
                await context.SaveChangesAsync();
                return plateforme.Id;
            });
        }

        /// <summary>Client de référence, rattaché à une plateforme dédiée.</summary>
        public async Task<int> CreateClientAsync(string suffixe = "")
        {
            return await WithDbAsync(async context =>
            {
                var client = new Client
                {
                    Nom = "Client test " + suffixe + Guid.NewGuid().ToString("N")[..6],
                    Email = $"client.{Guid.NewGuid():N}@ims.test",
                    PlateformeId = await CreatePlateformeAsync(suffixe),
                    EstActif = true
                };
                context.Clients.Add(client);
                await context.SaveChangesAsync();
                return client.Id;
            });
        }

        /// <summary>Commande client rattachée à un Client réellement persisté.</summary>
        public async Task<int> CreateCommandeClientAsync(string suffixe = "")
        {
            return await WithDbAsync(async context =>
            {
                var commande = new CommandeClient
                {
                    NumeroCommande = "CMD-TEST-" + Guid.NewGuid().ToString("N")[..8],
                    TitreCommande = "Commande de test",
                    ClientId = await CreateClientAsync(suffixe),
                    DateCommande = DateTime.Now,
                    Statut = StatutCommande.EnProduction
                };
                context.CommandesClients.Add(commande);
                await context.SaveChangesAsync();
                return commande.Id;
            });
        }

        /// <summary>Groupe de commandes : scope de stock alternative.</summary>
        public async Task<int> CreateGroupeCommandeAsync(string suffixe = "")
        {
            return await WithDbAsync(async context =>
            {
                var groupe = new GroupeCommande { DateCreation = DateTime.UtcNow };
                context.GroupesCommandes.Add(groupe);
                await context.SaveChangesAsync();
                return groupe.Id;
            });
        }

        /// <summary>Modifie les droits du rôle d'un utilisateur (droits de ressource).</summary>
        public async Task SetRoleFlagsAsync(
            string userId,
            bool? peutVoirTaches = null,
            bool? peutGererTaches = null,
            bool? peutVoirToutesTaches = null,
            bool? peutAssignerTaches = null,
            bool? peutVoirCourriels = null,
            bool? peutGererCourriels = null,
            bool? estAdministrateur = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var role = await context.AppRoles
                .Include(r => r.Utilisateurs)
                .FirstAsync(r => r.Utilisateurs.Any(u => u.Id == userId));

            if (peutVoirTaches.HasValue) role.PeutVoirTaches = peutVoirTaches.Value;
            if (peutGererTaches.HasValue) role.PeutGererTaches = peutGererTaches.Value;
            if (peutVoirToutesTaches.HasValue) role.PeutVoirToutesTaches = peutVoirToutesTaches.Value;
            if (peutAssignerTaches.HasValue) role.PeutAssignerTaches = peutAssignerTaches.Value;
            if (peutVoirCourriels.HasValue) role.PeutVoirCourriels = peutVoirCourriels.Value;
            if (peutGererCourriels.HasValue) role.PeutGererCourriels = peutGererCourriels.Value;
            if (estAdministrateur.HasValue) role.EstAdministrateur = estAdministrateur.Value;

            await context.SaveChangesAsync();
        }

        /// <summary>Exécute une action sur le DbContext de l'application.</summary>
        public async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await action(context);
        }

        /// <summary>
        /// Client HTTP porteur d'un JWT réel : l'authentification et les filtres
        /// d'autorisation de l'application sont donc réellement traversés.
        /// </summary>
        private HttpClient CreateAuthenticatedClient(string token)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        /// <summary>
        /// Exécute une action sur un contexte neuf et renvoie son résultat.
        /// Le type de retour n'est pas nullable : un SingleAsync/FirstAsync des tests
        /// est attendu comme présent, et le compilateur doit le vérifier.
        /// </summary>
        public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await action(context);
        }
    }

    public sealed class TestUser
    {
        public string Id { get; }
        public string Nom { get; }
        public string Prenom { get; }
        public HttpClient Client { get; }

        public TestUser(string id, string nom, string prenom, HttpClient client)
        {
            Id = id;
            Nom = nom;
            Prenom = prenom;
            Client = client;
        }
    }
}
