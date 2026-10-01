using System.Security.Cryptography;
using System.Text;

namespace Backend_Gestion_Magasin_API.Services.Partage
{
    /// <summary>
    /// Génération et empreinte du token de partage.
    /// </summary>
    public static class ShareTokenGenerator
    {
        /// <summary>32 octets d'aléa cryptographique = 256 bits.</summary>
        private const int TailleOctets = 32;

        /// <summary>
        /// Token URL-safe : base64url sans padding (43 caractères).
        /// </summary>
        /// <remarks>
        /// Aléa de <see cref="RandomNumberGenerator"/>, pas <c>Random</c> : un token
        /// prévisible rendrait le lien devinable. Le résultat vit dans un fragment
        /// d'URL (/partage#TOKEN), donc il doit tenir sans encodage supplémentaire :
        /// base64url n'utilise que [A-Za-z0-9_-], sûrs en URL et en fragment.
        /// </remarks>
        public static string Generer()
        {
            var octets = RandomNumberGenerator.GetBytes(TailleOctets);
            return Convert.ToBase64String(octets)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Empreinte SHA-256 du token, en hex minuscule (64 caractères).
        /// </summary>
        /// <remarks>
        /// C'est la SEULE représentation persistée. SHA-256 sans sel suffit ici :
        /// contrairement à un mot de passe, le token est un aléa de 256 bits — il n'a
        /// pas de faible entropie à protéger contre une attaque par dictionnaire, et
        /// l'absence de sel permet une recherche par égalité sur index unique.
        /// </remarks>
        public static string Hasher(string token)
        {
            var octets = Encoding.UTF8.GetBytes(token);
            return Convert.ToHexString(SHA256.HashData(octets)).ToLowerInvariant();
        }
    }
}