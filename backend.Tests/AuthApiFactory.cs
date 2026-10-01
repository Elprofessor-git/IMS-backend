using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Backend.Tests;

/// <summary>
/// <see cref="CapturingEmailSender"/> : capture les emails au lieu de les envoyer.
/// </summary>
/// <remarks>
/// On ne teste pas l'envoi réseau — ni Resend, ni journalisation : on teste ce qui
/// est observable côté API (codes HTTP, état en base, invalidation de session). La
/// capture permet en revanche de RÉCUPÉRER le jeton d'un lien, ce qui est la seule
/// façon d'exercer le flux « email → reset » de bout en bout sans boîte mail réelle.
/// </remarks>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailCapture> _envoys = new();
    private readonly ILogger<CapturingEmailSender> _logger;

    public CapturingEmailSender(ILogger<CapturingEmailSender> logger) => _logger = logger;

    /// <summary>Force l'échec de l'envoi, pour tester que le flux appelant survit.</summary>
    public bool DoitEchouer { get; set; }

    public IReadOnlyList<EmailCapture> Envoys
    {
        get { lock (_envoys) return _envoys.ToList(); }
    }

    public Task SendAsync(string to, string subject, string htmlBody)
    {
        lock (_envoys) _envoys.Add(new EmailCapture(to, subject, htmlBody));

        if (DoitEchouer)
            throw new EmailEnvoyeException("Échec simulé de l'envoi d'email.");

        return Task.CompletedTask;
    }

    /// <summary>Page de consommation, alignée sur PasswordSetupLinkService.CheminPage.</summary>
    private const string CheminPageAttendu = "/reset-password";

    /// <summary>
    /// Dernier lien de mot de passe capturé, paramètres analysés.
    /// </summary>
    /// <remarks>
    /// Le jeton est extrait de l'URL présente dans le corps HTML, exactement comme le
    /// ferait un utilisateur qui clique sur le lien de l'email : le test ne contourne
    /// donc pas la génération de jeton d'Identity, il ne fait que lire le canal.
    /// </remarks>
    public Uri? DernierLien(string? pourDestinataire = null)
    {
        var capture = Envoys.LastOrDefault(e => pourDestinataire is null ||
                                                e.Destinataire.Equals(pourDestinataire, StringComparison.OrdinalIgnoreCase));
        if (capture is null) return null;

        // Le corps HTML place le lien deux fois (href du bouton, puis en texte de repli).
        // On part de la page cible et on remonte jusqu'au début de l'URL : chercher
        // "?userId=" directement ne donnerait qu'une chaîne relative, illisible par
        // Uri, et raterait le cas où Email:FrontendBaseUrl n'est pas configurée.
        var page = capture.Corps.IndexOf(CheminPageAttendu, StringComparison.Ordinal);
        if (page < 0) return null;

        var debut = capture.Corps.LastIndexOf("http", page, StringComparison.Ordinal);
        if (debut < 0) return null;

        var fin = capture.Corps.IndexOf('"', page);
        if (fin < 0) return null;

        // Le corps HTML échappe les caractères ; on annule l'échappement avant d'analyser.
        var brut = System.Net.WebUtility.HtmlDecode(capture.Corps[debut..fin]);
        return Uri.TryCreate(brut, UriKind.Absolute, out var uri) ? uri : null;
    }
}

public sealed record EmailCapture(string Destinataire, string Objet, string Corps);

/// <summary>
/// Fabrique d'hôte pour les tests d'authentification.
/// </summary>
/// <remarks>
/// Reprend le harnais PostgreSQL réel de <c>TacheApiFactory</c> (base éphémère créée
/// par les MIGRATIONS) et y substitue l'émetteur d'email par un émetteur capturant.
/// Aucun appel réseau n'est donc possible depuis ces tests, quelle que soit la
/// configuration machine.
/// </remarks>
public class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Clé de signature PARTAGÉE avec <see cref="TacheApiFactory"/>, et non propre à
    /// cette classe.
    /// </summary>
    /// <remarks>
    /// Program.cs et TokenService lisent <c>JWT_SECRET</c> dans l'ENVIRONNEMENT DU
    /// PROCESSUS, qui est global : deux constantes concurrentes signifient que la
    /// dernière initialisation statique gagne. Un hôte déjà démarré avec la clé A se
    /// retrouve alors à VALIDER des jetons signés avec la clé B → 401 aléatoire, qui
    /// ne se manifeste qu'en exécution complète (chaque classe de tests passe isolément).
    /// Une seule clé, posée une fois, supprime la course.
    /// </remarks>
    public const string TestJwtSecret = TacheApiFactory.TestJwtSecret;

    /// <summary>
    /// Durée de vie du jeton de lien, forcée dans le conteneur de test.
    /// </summary>
    /// <remarks>
    /// <c>null</c> = réglage de production (1 h). <see cref="TimeSpan.Zero"/> fait
    /// expirer le jeton à l'instant de sa création : c'est le seul moyen d'exercer le
    /// cas « lien périmé » sans attendre une heure réelle ni introduire de faux
    /// horloge. Une instance dédiée est utilisée pour ce test, afin de ne pas rendre
    /// tous les autres jetons expirés.
    /// </remarks>
    public TimeSpan? DureeLien { get; init; }

    static AuthApiFactory()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", TestJwtSecret);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", "SystemeGestionTextile");
        Environment.SetEnvironmentVariable("JwtSettings__Audience", "SystemeGestionTextileUsers");
        // Aucune clé d'email en test : aucun appel réseau, quelle que soit la machine
        // qui exécute la suite.
        Environment.SetEnvironmentVariable("RESEND_API_KEY", null);
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
                ["JwtSettings:Secret"] = TestJwtSecret,
                // Base du frontend explicite : sans elle, le lien généré serait relatif
                // et illisible par Uri. La valeur n'affecte que la forme de l'URL.
                ["Email:FrontendBaseUrl"] = "http://localhost:3000",
            });

            if (DureeLien is { } duree)
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Identity:PasswordResetTokenLifespan"] = duree.ToString(),
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_database.ConnectionString, npgsql => npgsql.EnableRetryOnFailure()));

            // Les DEUX enregistrements sont nécessaires : le second seul rendrait
            // CapturingEmailSender introuvable pour les assertions (AddScoped
            // n'enregistre pas le type concret, seulement l'interface résolue).
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<CapturingEmailSender>();
            // SINGLETON, et non scoped : les assertions interrogent la capture APRÈS
            // l'appel HTTP, depuis un scope différent. En scoped, chaque requête
            // obtiendrait une instance neuve et la liste resterait vide — les tests
            // passeraient à vide, sans rien vérifier.
            services.AddSingleton<CapturingEmailSender>();
            services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
        });
    }

    public async Task InitializeAsync()
    {
        using var probe = CreateClient();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Émetteur capturant du conteneur de l'hôte.</summary>
    public CapturingEmailSender EmailSender()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CapturingEmailSender>();
    }

    public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(context);
    }

    public async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(context);
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    public Task WithUserManagerAsync(Func<UserManager<ApplicationUser>, Task> action)
        => WithScopeAsync(sp => action(sp.GetRequiredService<UserManager<ApplicationUser>>()));

    public Task<T> WithUserManagerAsync<T>(Func<UserManager<ApplicationUser>, Task<T>> action)
        => WithScopeAsync(sp => action(sp.GetRequiredService<UserManager<ApplicationUser>>()));

    public async Task<Role> CreateRoleAsync(string nom, Action<Role>? configure = null)
        => await WithDbAsync(async context =>
        {
            var role = new Role { NomRole = nom, Description = "Rôle de test", EstActif = true };
            configure?.Invoke(role);
            context.AppRoles.Add(role);
            await context.SaveChangesAsync();
            return role;
        });

    /// <summary>
    /// Crée un utilisateur avec un mot de passe CONNU, et renvoie un client portant un
    /// JWT valide pour lui. Sert de point de départ aux scénarios « déjà connecté ».
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email,
        string motDePasse = AuthFlowTests.MotDePasse,
        int? roleId = null,
        bool estActif = true)
    {
        await WithUserManagerAsync(async um =>
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EstActif = estActif,
                EmailConfirmed = true,
                RoleId = roleId,
            };
            var result = await um.CreateAsync(user, motDePasse);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Création du compte de test impossible : " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        });

        var reponse = await PostAsync("/api/Auth/login", new { email, password = motDePasse });
        reponse.EnsureSuccessStatusCode();

        var token = JsonDocument.Parse(await reponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("token").GetString()!;

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public Task<HttpResponseMessage> PostAsync(string url, object corps)
        => CreateClient().PostAsJsonAsync(url, corps);

    public Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object corps)
        => client.PostAsJsonAsync(url, corps);

    /// <summary>
    /// PUT JSON authentifié. L'API d'administration des comptes n'expose que PUT —
    /// un helper POST vers cette route répondrait 405 et ferait échouer le test sur une
    /// détail de verb sans jamais atteindre la logique testée.
    /// </summary>
    public Task<HttpResponseMessage> PutAsync(HttpClient client, string url, object corps)
        => client.PutAsJsonAsync(url, corps);

    /// <summary>
    /// Corps de la réponse, décodé.
    /// </summary>
    /// <remarks>
    /// Les assertions portent sur le MESSAGE, pas sur l'enveloppe JSON : comparer la
    /// chaîne brute ferait échouer le test sur un simple changement de casse de
    /// sérialisation, et surtout Testerait la forme du transport plutôt que la
    /// propriété de sécurité recherchée.
    /// </remarks>
    public static async Task<string> BodyAsync(HttpResponseMessage reponse)
    {
        var brut = await reponse.Content.ReadAsStringAsync();
        try
        {
            var racine = JsonDocument.Parse(brut).RootElement;
            return racine.ValueKind == JsonValueKind.Object &&
                   racine.TryGetProperty("message", out var message)
                ? message.GetString() ?? string.Empty
                : brut;
        }
        catch (JsonException)
        {
            return brut;
        }
    }

    /// <summary>Le lien de mot de passe du DERNIER email, paramètres userId/token extraits.</summary>
    public Task<(string UserId, string Token)?> DernierLienAsync(string destinataire)
    {
        var lien = EmailSender().DernierLien(destinataire);
        if (lien is null) return Task.FromResult<(string, string)?>(null);

        var q = QueryHelpers.ParseQuery(lien.Query);
        return Task.FromResult<(string UserId, string Token)?>((q["userId"].ToString(), q["token"].ToString()));
    }
}