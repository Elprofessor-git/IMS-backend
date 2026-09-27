using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models.Gmail;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    // Scopes minimaux pour la V1 : lecture + gestion des brouillons/envoi (pas de suppression/modification de labels).
    // https://www.googleapis.com/auth/gmail.modify n'est PAS demandé : on reste sur readonly + compose.
    public static class GmailScopes
    {
        public const string Readonly = "https://www.googleapis.com/auth/gmail.readonly";
        public const string Compose = "https://www.googleapis.com/auth/gmail.compose";
        public const string Email = "https://www.googleapis.com/auth/userinfo.email";
        public const string All = Readonly + " " + Compose + " " + Email;
    }

    public interface IGmailOAuthService
    {
        /// <summary>False si ClientId/ClientSecret/RedirectUri manquent : permet un message d'erreur clair côté UI.</summary>
        bool IsConfigured { get; }
        string BuildAuthorizationUrl(string userId);
        Task<GmailConnection> HandleCallbackAsync(string code, string state);
        string BuildPostCallbackRedirectUrl(bool success, string? errorMessage = null);
        /// <summary>Best-effort : révoque le refresh token côté Google sans faire échouer l'appelant.</summary>
        Task RevokeAsync(GmailConnection connection);
        Task<string> GetFreshAccessTokenAsync(GmailConnection connection);
    }

    public class GmailOAuthService : IGmailOAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITokenEncryptionService _encryption;
        private readonly IDataProtector _stateProtector;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GmailOAuthService> _logger;

        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
        private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

        public GmailOAuthService(
            ApplicationDbContext context,
            ITokenEncryptionService encryption,
            IDataProtectionProvider dataProtectionProvider,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GmailOAuthService> logger)
        {
            _context = context;
            _encryption = encryption;
            // Purpose distinct pour cette feature : un state Gmail ne peut pas être rejoué ailleurs.
            _stateProtector = dataProtectionProvider.CreateProtector("Gmail.OAuthState.v1");
            _httpClient = httpClientFactory.CreateClient("gmail");
            _configuration = configuration;
            _logger = logger;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_configuration["Google:ClientId"])
            && !string.IsNullOrWhiteSpace(_configuration["Google:ClientSecret"])
            && !string.IsNullOrWhiteSpace(_configuration["Google:RedirectUri"]);

        private string ClientId => _configuration["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId manquant (variable d'environnement Google__ClientId).");
        private string ClientSecret => _configuration["Google:ClientSecret"]
            ?? throw new InvalidOperationException("Google:ClientSecret manquant (variable d'environnement Google__ClientSecret).");
        private string RedirectUri => _configuration["Google:RedirectUri"]
            ?? throw new InvalidOperationException("Google:RedirectUri manquant (ex: http://localhost:5000/api/gmail/callback).");
        private string FrontendMailUrl => _configuration["Google:FrontendMailUrl"]
            ?? "http://localhost:3000";

        // Le state encode userId + nonce + timestamp, signé (IDataProtector) — jamais un simple GUID en clair.
        // Indispensable car la redirection Google->callback n'a PAS le cookie JWT httpOnly du frontend
        // (le callback touche le backend directement, pas via le proxy Next.js).
        public string BuildAuthorizationUrl(string userId)
        {
            var payload = JsonSerializer.Serialize(new
            {
                userId,
                nonce = Guid.NewGuid().ToString("N"),
                issuedAtUtc = DateTime.UtcNow
            });
            var state = _stateProtector.Protect(payload);

            var query = QueryString.Create(new Dictionary<string, string?>
            {
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["response_type"] = "code",
                ["scope"] = GmailScopes.All,
                ["access_type"] = "offline",   // requis pour obtenir un refresh_token
                ["prompt"] = "consent",        // force le renvoi du refresh_token même en reconnexion
                ["state"] = state
            });

            return $"{AuthEndpoint}{query}";
        }

        public async Task<GmailConnection> HandleCallbackAsync(string code, string state)
        {
            var userId = ReadUserIdFromState(state);

            // Échange code -> tokens
            using var tokenResponse = await _httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["redirect_uri"] = RedirectUri,
                ["grant_type"] = "authorization_code"
            }));

            var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Échange de code OAuth Gmail échoué : {Body}", tokenBody);
                throw new InvalidOperationException("Google a refusé le code d'autorisation. Reprenez la connexion.");
            }

            using var tokenDoc = JsonDocument.Parse(tokenBody);
            var root = tokenDoc.RootElement;
            var accessToken = root.GetProperty("access_token").GetString()!;
            var scope = root.TryGetProperty("scope", out var s) ? s.GetString() ?? GmailScopes.All : GmailScopes.All;

            if (!root.TryGetProperty("refresh_token", out var refreshTokenElement) || string.IsNullOrEmpty(refreshTokenElement.GetString()))
            {
                // Arrive si l'utilisateur avait déjà consenti sans access_type=offline auparavant ;
                // prompt=consent force normalement Google à le renvoyer, mais on protège quand même.
                throw new InvalidOperationException(
                    "Google n'a pas renvoyé de refresh_token. Déconnectez l'application depuis myaccount.google.com/permissions puis réessayez.");
            }
            var refreshToken = refreshTokenElement.GetString()!;

            // Identité du compte Gmail connecté
            using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            using var userInfoResponse = await _httpClient.SendAsync(userInfoRequest);
            if (!userInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Lecture userinfo Gmail échouée : {Body}", await userInfoResponse.Content.ReadAsStringAsync());
                throw new InvalidOperationException("Impossible de lire l'identité du compte Google connecté.");
            }
            using var userInfoDoc = JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync());
            var gmailAddress = userInfoDoc.RootElement.GetProperty("email").GetString()!;
            var googleUserId = userInfoDoc.RootElement.GetProperty("sub").GetString()!;

            var encrypted = _encryption.Encrypt(refreshToken);

            var existing = await _context.GmailConnections
                .FirstOrDefaultAsync(c => c.UserId == userId && c.GoogleUserId == googleUserId);

            if (existing != null)
            {
                existing.RefreshTokenEncrypted = encrypted;
                existing.GrantedScopes = scope;
                existing.GmailAddress = gmailAddress;
                existing.IsActive = true;
                existing.DisconnectedAt = null;
                existing.ConnectedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return existing;
            }

            var connection = new GmailConnection
            {
                UserId = userId,
                GmailAddress = gmailAddress,
                GoogleUserId = googleUserId,
                RefreshTokenEncrypted = encrypted,
                GrantedScopes = scope,
                ConnectedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.GmailConnections.Add(connection);
            await _context.SaveChangesAsync();
            return connection;
        }

        // Lecture + validation du state signé (protection anti-CSRF + anti-rejeu par expiration).
        private string ReadUserIdFromState(string state)
        {
            string payloadJson;
            try
            {
                payloadJson = _stateProtector.Unprotect(state);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("État OAuth invalide ou altéré (protection anti-CSRF). Recommencez la connexion.", ex);
            }

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                if (!doc.RootElement.TryGetProperty("userId", out var userIdElement) || string.IsNullOrEmpty(userIdElement.GetString()))
                    throw new InvalidOperationException("État OAuth incomplet (userId absent). Recommencez la connexion.");

                var issuedAt = doc.RootElement.GetProperty("issuedAtUtc").GetDateTimeOffset();
                if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromMinutes(10))
                    throw new InvalidOperationException("Le lien de connexion Gmail a expiré, recommencez.");

                return userIdElement.GetString()!;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("État OAuth illisible. Recommencez la connexion.", ex);
            }
        }

        public string BuildPostCallbackRedirectUrl(bool success, string? errorMessage = null)
        {
            var url = $"{FrontendMailUrl.TrimEnd('/')}/courriels/retour?success={success.ToString().ToLowerInvariant()}";
            if (!success && errorMessage != null)
                url += $"&error={Uri.EscapeDataString(errorMessage)}";
            return url;
        }

        /// <summary>
        /// Révoque le refresh token côté Google (best-effort) puis désactive la connexion.
        /// Une révocation en échec ne bloque pas la déconnexion locale.
        /// </summary>
        public async Task RevokeAsync(GmailConnection connection)
        {
            try
            {
                var refreshToken = _encryption.Decrypt(connection.RefreshTokenEncrypted);
                using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken });
                using var response = await _httpClient.PostAsync(RevokeEndpoint, content);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Révocation du refresh token Gmail non aboutie pour la connexion {Id} : {Status}", connection.Id, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Révocation du refresh token Gmail impossible pour la connexion {Id} (déconnexion locale maintenue).", connection.Id);
            }
        }

        // Un access_token Gmail expire en ~1h : on en redemande un frais à chaque opération plutôt
        // que de le mettre en cache (plus simple, pas de table supplémentaire ; le refresh_token, lui, ne change pas).
        public async Task<string> GetFreshAccessTokenAsync(GmailConnection connection)
        {
            var refreshToken = _encryption.Decrypt(connection.RefreshTokenEncrypted);

            using var response = await _httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["grant_type"] = "refresh_token"
            }));

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Rafraîchissement du token Gmail échoué pour la connexion {ConnectionId} : {Body}", connection.Id, body);

                // invalid_grant = l'utilisateur a révoqué l'accès côté Google -> on désactive la connexion
                if (body.Contains("invalid_grant"))
                {
                    connection.IsActive = false;
                    connection.DisconnectedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    throw new InvalidOperationException("L'accès Gmail a été révoqué côté Google. Reconnectez le compte.");
                }
                throw new InvalidOperationException("Impossible de rafraîchir l'accès Gmail.");
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }
    }
}
