using Microsoft.AspNetCore.Identity;
using Backend_Gestion_Magasin_API.Models;
using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services.Auth;
using Backend_Gestion_Magasin_API.Services.Email;
using Backend_Gestion_Magasin_API.Services.Partage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// Fix Render (Bug 13) : désactiver le rechargement à chaud AVANT CreateBuilder.
// C'est CreateBuilder qui charge appsettings.json en interne et crée le FileSystemWatcher
// (crash « inotify instances limit (128) reached » sur Render) — le Sources.Clear() seul,
// exécuté après, agissait trop tard : le watcher était déjà créé.
Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");

// Chargement du .env par le paquet déjà présent dans le projet (dotenv.net, référencé
// dans le .csproj). L'appel n'existait nulle part dans le code : la dépendance était
// déclarée mais jamais câblée, et le .env du backend n'était donc jamais lu — il fallait
// exporter les variables à la main. Or `source .env` ne fonctionne pas ici : la chaîne de
// connexion contient des points-virgules que le shell prend pour des séparateurs de
// commandes, et la variable arrivait tronquée à « Host=localhost », sans base ni mot de
// passe. Le paquet, lui, préserve les points-virgules.
//
// WithoutOverwriteExistingVars() n'est pas cosmétique : le défaut du paquet est
// OverwriteExistingVars = True. Comme backend/Dockerfile fait « COPY . . » sans
// .dockerignore, le backend/.env est recopié dans l'image ; sans cette option, il
// écraserait les variables réellement injectées par le env_file de docker-compose, en
// production compris. Les variables déjà présentes dans l'environnement gagnent donc
// toujours, et le fichier ne sert qu'en développement local.
try
{
    var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(envFile))
    {
        DotEnv.Load(new DotEnvOptions()
            .WithEnvFiles(envFile)
            .WithoutOverwriteExistingVars());
    }
}
catch (Exception ex)
{
    // Un .env illisible ne doit pas empêcher l'API de démarrer : en conteneur, la
    // configuration arrive par l'environnement. On le signale et on continue.
    Console.WriteLine($"Lecture du .env ignorée : {ex.Message}");
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Charger appsettings.json, appsettings.{env}.json, les arguments CLI et les variables d'environnement
// NOTE (fix Render) : reloadOnChange désactivé pour éviter les watchers inotify (limite 128 sur Render).
// La config ne se recharge plus à chaud : un changement nécessite un redémarrage/redéploiement.
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddCommandLine(args)
    .AddEnvironmentVariables();

// Lire la connexion DB (depuis appsettings OU variable d'environnement)
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"];

Console.WriteLine($"Connection string utilise: {connectionString}");

// Configure Npgsql pour DateTime
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure());
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Durée de vie d'un lien « mot de passe oublié » / « choisir mon mot de passe ».
//
// .NET 9 a DÉPLACÉ ce réglage : `TokenOptions.PasswordResetTokenLifespan` n'existe
// plus, la durée est une option du fournisseur de jetons lui-même
// (`DataProtectionTokenProviderOptions.TokenLifespan`, défaut 24 h). Le setter sur
// l'ancien emplacement ne compile pas — c'est vérifié, pas supposé.
// 24 h serait igual à la durée de vie du JWT lui-même : un lien de réinitialisation
// resterait alors utilisable aussi longtemps qu'un jeton de session, dans la boîte
// mail du destinataire. Une heure est le compromis habituel : assez pour un
// utilisateur absent de son poste, trop court pour un lien laissé dans une boîte
// partagée. Le jeton reste à usage unique (il embarque le SecurityStamp).
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    // Lisible depuis la configuration pour une seule raison : rendre le test
    // « jeton expiré » réalisable sans attendre une heure. Une durée nulle fait expirer
    // le jeton à l'instant même de sa création — c'est le plus court délai qu'on puisse
    // observer. Le défaut reste d'une heure.
    var duree = builder.Configuration["Identity:PasswordResetTokenLifespan"];
    options.TokenLifespan = TimeSpan.TryParse(duree, out var d) && d >= TimeSpan.Zero
        ? d
        : TimeSpan.FromHours(1);
});

// Variable d'environnement en premier : c'est elle que le .env vient alimenter juste
// au-dessus, et celle qu'injectent docker-compose et les plateformes de déploiement.
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["JwtSettings:Secret"];
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "sgt-app";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "sgt-users";

if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT Secret is not configured. Please set JwtSettings:Secret in appsettings.json or JWT_SECRET environment variable.");
}

var key = Encoding.UTF8.GetBytes(jwtSecret);

// HMAC-SHA256 exige une clé d'au moins 256 bits. Sans ce contrôle, l'API démarre
// normalement puis renvoie un 500 non géré à la PREMIÈRE connexion, sans que rien
// n'indique que le secret est en cause : on perd du temps à chercher le problème
// dans l'authentification ou la base. On échoue donc au démarrage, avec la cause.
if (key.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT Secret is too short: {key.Length * 8} bits provided, but HMAC-SHA256 requires at least 256 bits (32 characters). " +
        "Use a random value of at least 32 characters, e.g. `openssl rand -base64 32`.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };

    // Révocation des sessions SANS liste de révocation et SANS refresh token : à chaque
    // requête authentifiée, on relit le SecurityStamp du compte et on le compare à la
    // claim du jeton. Un mot de passe changé/réinitialisé, un compte désactivé ou un
    // ou rôle modifié change ce stamp nativement (Identity, ou
    // UserController.Update) → le jeton devient inutilisable sur-le-champ.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (context.Principal?.Identity?.IsAuthenticated != true)
                return;

            var validation = context.HttpContext.RequestServices
                .GetRequiredService<ISessionValidationService>();

            var resultat = await validation.ValiderAsync(context.Principal);

            // context.Fail → 401. Le motif n'est jamais renvoyé au client (il est
            // journalisé côté serveur) : un attaquant ne doit pas distinguer
            // « compte désactivé » de « session révoquée ».
            if (!resultat.EstValide)
                context.Fail(resultat.Motif ?? "Session invalide.");
        }
    };
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:4200",
            "https://localhost:4200",
            "https://ims-frontend-sage.vercel.app",
            "https://ims-backend-g95v.onrender.com"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();        
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// IP réel derrière un proxy (rate limiting du partage public)
// ─────────────────────────────────────────────────────────────────────────────
//
// Le backend est appelé par le proxy Next (Vercel) puis, en production, par le
// répartiteur Render : sans lecture de X-Forwarded-For, Connection.RemoteIpAddress
// vaut l'IP du proxy pour TOUT LE MONDE, et un limiteur par IP ne limiterait alors
// qu'une seule partition partagée par tous les visiteurs.
//
// KnownProxies/KnownNetworks sont vidés volontairement : on ne connaît pas l'IP
// exacte du proxy à l'avance (elle change chez Render). Le middleware prend donc la
// valeur la plus à droite de X-Forwarded-For, censée être posée par le dernier
// intermédiaire de confiance. Limite assumée : un appel DIRECT au backend (sans
// proxy) permettrait de forger l'entête et de choisir sa partition — le limiteur est
// une défense en profondeur, pas le contrôle d'accès (celui-ci reste l'empreinte du
// token, imprévisible). Voir le rapport de lot.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Limiteur dédié au point d'entrée public. Clé = IP réelle (après forwarded headers).
// Le seuil est lu via l'IConfiguration de la requête (et non builder.Configuration) :
// en test, la configuration ajoutée par la fabrique n'est visible qu'après Build, or
// builder.Configuration est lu AVANT. Le défaut (30/min) est généreux pour un usage
// humain (ouvrir un lien), serré pour une énumération de tokens.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("partage-public", httpContext =>
    {
        var limite = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue<int?>("Partage:RateLimitParMinute") ?? 30;

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "inconnue";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limite,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

// Register custom services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<TokenService>();

// Authentification — emails de mot de passe et validation de session.
//
// IEmailSender : Resend si une clé est configurée, sinon un émetteur qui journalise.
// Le choix se fait sur IConfiguration (présence de RESEND_API_KEY), jamais sur
// #if DEBUG : un environnement de recette sans clé doit continuer à démarrer, et une
// suite de tests ne doit jamais faire d'appel réseau vers un service externe.
builder.Services.AddHttpClient(ResendEmailSender.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "IMS-Backend/2.0 (emails transactionnels)");
});

var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")
                   ?? builder.Configuration["Resend:ApiKey"];

if (!string.IsNullOrWhiteSpace(resendApiKey))
{
    builder.Services.AddScoped<IEmailSender, ResendEmailSender>();
    Console.WriteLine("Emails transactionnels : Resend (RESEND_API_KEY détectée).");
}
else
{
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
    Console.WriteLine("Emails transactionnels : émetteur journalisé (aucune RESEND_API_KEY configurée).");
}

builder.Services.AddScoped<IPasswordSetupLinkService, PasswordSetupLinkService>();
builder.Services.AddScoped<ISessionValidationService, SessionValidationService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<CommandeService>();
builder.Services.AddScoped<ImportationService>();
builder.Services.AddScoped<FournisseurClientService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<QualiteService>();

// Partage sécurisé — résolution de périmètre puis service de liens.
builder.Services.AddScoped<ShareScopeResolver>();
builder.Services.AddScoped<IShareLinkService, ShareLinkService>();

// Identité de l'utilisateur courant (claims JWT) et règles d'ownership du module Tâches.
// ICurrentUserService est la seule source d'identité serveur : le frontend ne fournit
// jamais l'Id du propriétaire d'une ressource.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITacheOwnershipService, TacheOwnershipService>();

// Cloche de notifications — service partagé par les nouveaux émetteurs (tâches, email).
// Mêmes table, même API et même faîte « cloche » que l'émetteur historique du planning
// (PlanningController.NotifierAsync, volontairement laissé tel quel) : aucun second
// système de notification n'est introduit.
builder.Services.AddScoped<INotificationService, NotificationService>();

// Chatbot IA — HttpClient typé pour GroqService, puis ToolExecutor et ChatbotAgentService
builder.Services.AddHttpClient<GroqService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("User-Agent", "IMS-Backend/2.0");
});
builder.Services.AddScoped<ToolExecutor>();
builder.Services.AddScoped<ChatbotAgentService>();

// Module Courriels (Gmail) — clients HTTP nommés + services.
// « gmail » : appels Google (OAuth + API REST) — timeout large, la synchro récupère plusieurs messages.
// « groq »  : assistant IA des emails, sur le même modèle que GroqService (partage l'API key).
builder.Services.AddHttpClient("gmail", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "IMS-Backend/2.0 (module courriels)");
});
builder.Services.AddHttpClient("groq", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Singleton : la clé AES est lue une seule fois au démarrage. L'injection est paresseuse,
// le module reste donc utilisable même si la clé manque (erreur explicite au premier envoi).
builder.Services.AddSingleton<ITokenEncryptionService, TokenEncryptionService>();
builder.Services.AddScoped<IGmailOAuthService, GmailOAuthService>();
builder.Services.AddScoped<IGmailApiService, GmailApiService>();
builder.Services.AddScoped<IGmailSyncService, GmailSyncService>();
builder.Services.AddScoped<IGmailAiService, GmailAiService>();
// Verrou de synchronisation partagé entre le service de fond et les synchronisations
// manuelles : le singleton est obligatoire, sinon les deux ne se verraient pas.
builder.Services.AddSingleton<IGmailSyncGate, GmailSyncGate>();

// Synchronisation automatique : options « Gmail:AutoSync » + service de fond.
// Lisible par variable d'environnement : Gmail__AutoSync__IntervalMinutes=10.
builder.Services.Configure<GmailAutoSyncOptions>(builder.Configuration.GetSection(GmailAutoSyncOptions.SectionName));
builder.Services.AddHostedService<GmailAutoSyncService>();

var app = builder.Build();

// En PREMIER : réécrit Connection.RemoteIpAddress depuis X-Forwarded-For. Doit
// précéder le limiteur et le redirigeur HTTPS, qui lisent tous deux l'IP/schéma.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only use HTTPS redirection when not in container
var isInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                   Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Contains("http://") == true;

if (!isInContainer)
{
    app.UseHttpsRedirection();
}

// ✅ AJOUTER CETTE LIGNE - Obligatoire pour que CORS fonctionne
app.UseRouting();
app.UseStaticFiles();

// Après UseRouting (les politiques par endpoint sont résolues) et avant
// l'autorisation : le point public est anonyme, mais reste limité par IP.
app.UseRateLimiter();

// Use CORS
app.UseCors("AllowFrontend");

// Use Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers()
        .RequireCors("AllowFrontend");

app.MapHub<Backend_Gestion_Magasin_API.Hubs.PlanningHub>("/hubs/planning")
        .RequireCors("AllowFrontend")
        .RequireAuthorization(); 

// Ensure database creation and apply migrations (with retry)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    const int maxRetries = 5;
    var delay = TimeSpan.FromSeconds(5);

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();
            await SeedData.Initialize(services);
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex, "Database initialization failed (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                attempt, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed after {Max} attempts.", maxRetries);
            throw;
        }
    }
}

// ✅ Ajoutez ceci AVANT app.Run() dans Program.cs
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    time = DateTime.UtcNow 
})).AllowAnonymous();

app.Run();

// Point d'entrée exposé pour les tests d'intégration (WebApplicationFactory<Program>).
// Les instructions top-level génèrent une classe Program interne : sans cette
// déclaration partielle, la fabrique de tests ne peut pas démarrer l'hôte.
public partial class Program { }
