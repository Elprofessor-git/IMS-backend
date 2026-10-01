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

// Variable d'environnement en premier : c'est elle que le .env vient alimenter juste
// au-dessus, et celle qu'injectent docker-compose et les plateformes de déploiement.
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["JwtSettings:Secret"];
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "ims-app";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "ims-users";

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

// Register custom services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<CommandeService>();
builder.Services.AddScoped<ImportationService>();
builder.Services.AddScoped<FournisseurClientService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<QualiteService>();

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
