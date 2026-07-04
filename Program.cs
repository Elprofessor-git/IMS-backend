using Microsoft.AspNetCore.Identity;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Charger appsettings.json, appsettings.{env}.json et les variables d'environnement
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
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

// Configure JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? builder.Configuration["JwtSettings:Secret"];
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "ims-app";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "ims-users";

if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT Secret is not configured. Please set JwtSettings:Secret in appsettings.json or JWT_SECRET environment variable.");
}

var key = Encoding.UTF8.GetBytes(jwtSecret);

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
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<CommandeService>();
builder.Services.AddScoped<ImportationService>();
builder.Services.AddScoped<TacheService>();
builder.Services.AddScoped<FournisseurClientService>();
builder.Services.AddScoped<IArticleService, ArticleService>();

// Chatbot IA — HttpClient typé pour GroqService, puis ToolExecutor et ChatbotAgentService
builder.Services.AddHttpClient<GroqService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("User-Agent", "IMS-Backend/2.0");
});
builder.Services.AddScoped<ToolExecutor>();
builder.Services.AddScoped<ChatbotAgentService>();

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
