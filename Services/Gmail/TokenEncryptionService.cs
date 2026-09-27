using System.Security.Cryptography;
using System.Text;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    // Chiffrement symétrique (AES-GCM) du refresh token Gmail avant stockage PostgreSQL.
    // Clé lue depuis la configuration : Google:TokenEncryptionKey (32 octets en Base64)
    // -> en prod : variable d'environnement Google__TokenEncryptionKey, JAMAIS committée.
    public interface ITokenEncryptionService
    {
        /// <summary>Une clé AES-256 exploitable est-elle configurée ?</summary>
        bool IsAvailable { get; }

        string Encrypt(string plainText);
        string Decrypt(string cipherTextBase64);
    }

    public class TokenEncryptionService : ITokenEncryptionService
    {
        private readonly byte[]? _key;
        private readonly ILogger<TokenEncryptionService> _logger;

        // Une clé absente ou invalide ne doit pas empêcher l'API de démarrer : le module
        // Courriels reste consultable, seule la connexion Gmail est refusée. L'erreur est
        // remontée à l'utilisateur via ITokenEncryptionService.IsAvailable.
        public TokenEncryptionService(IConfiguration configuration, ILogger<TokenEncryptionService> logger)
        {
            _logger = logger;

            var keyBase64 = (configuration["Google:TokenEncryptionKey"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_TOKEN_ENCRYPTION_KEY")
                ?? string.Empty).Trim();

            if (keyBase64.Length == 0)
            {
                _logger.LogWarning(
                    "Google:TokenEncryptionKey absente : le module Courriels sera en lecture seule, la connexion Gmail est impossible.");
                return;
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(keyBase64);
            }
            catch (FormatException)
            {
                _logger.LogError("Google:TokenEncryptionKey n'est pas du Base64 valide : le module Courriels sera en lecture seule.");
                return;
            }

            if (key.Length != 32)
            {
                _logger.LogError(
                    "Google:TokenEncryptionKey doit décoder en exactement 32 octets (AES-256), obtenu {Length}. Le module Courriels sera en lecture seule.",
                    key.Length);
                return;
            }

            _key = key;
        }

        public bool IsAvailable => _key is not null;

        private byte[] Key => _key ?? throw new InvalidOperationException(
            "Le stockage des tokens Gmail n'est pas configuré (Google:TokenEncryptionKey). " +
            "Générer une clé AES-256 en Base64 (ex: openssl rand -base64 32) et la placer en " +
            "variable d'environnement Google__TokenEncryptionKey, jamais dans appsettings.json.");

        public string Encrypt(string plainText)
        {
            using var aes = new AesGcm(Key, AesGcm.TagByteSizes.MaxSize);
            var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Concaténation nonce + tag + cipher, encodée en Base64 pour stockage texte en DB
            var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

            return Convert.ToBase64String(combined);
        }

        public string Decrypt(string cipherTextBase64)
        {
            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            var tagSize = AesGcm.TagByteSizes.MaxSize;

            byte[] combined;
            try
            {
                combined = Convert.FromBase64String(cipherTextBase64);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Le refresh token stocké est illisible (format invalide). Reconnectez le compte Gmail.", ex);
            }

            if (combined.Length < nonceSize + tagSize)
                throw new InvalidOperationException("Le refresh token stocké est tronqué. Reconnectez le compte Gmail.");

            var nonce = combined[..nonceSize];
            var tag = combined[nonceSize..(nonceSize + tagSize)];
            var cipherBytes = combined[(nonceSize + tagSize)..];
            var plainBytes = new byte[cipherBytes.Length];

            using var aes = new AesGcm(Key, AesGcm.TagByteSizes.MaxSize);
            try
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }
            catch (CryptographicException ex)
            {
                // Tag invalide : la clé a changé (rotation, autre environnement) ou la donnée est altérée.
                _logger.LogError(ex, "Déchiffrement du refresh token Gmail impossible : clé différente ou donnée altérée.");
                throw new InvalidOperationException("Le refresh token stocké n'est plus déchiffrable (ch clé modifiée ?). Reconnectez le compte Gmail.", ex);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
