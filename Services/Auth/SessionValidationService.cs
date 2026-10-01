using System.Security.Claims;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Auth
{
    /// <summary>
    /// Vérifie, à CHAQUE requête authentifiée, que le JWT présenté correspond encore
    /// à un compte actif.
    ///
    /// Deux contrôles, une seule lecture en base (une requête par clé primaire sur
    /// AspNetUsers, projections ASM_AsNoTracking) :
    /// <list type="bullet">
    /// <item><b>SecurityStamp</b> : identique à celui porté par la claim du jeton.
    /// Identity le fait changer nativement sur ResetPasswordAsync et
    /// ChangePasswordAsync ; on ne le modifie donc jamais à la main dans ces flux.
    /// Toute divergence signifie « le compte a changé depuis l'émission du jeton »
    /// → 401.</item>
    /// <item><b>EstActif</b> : un compte désactivé perd l'accès immédiatement, sans
    /// attendre l'expiration du jeton. Avant ce lot, EstActif n'était lu NULLE PART à
    /// la connexion : un compte désactivé pouvait se connecter et continuer à
    /// travailler jusqu'à l'expiration de son jeton.</item>
    /// </list>
    ///
    /// Volontairement SANS cache : un cache, même de quelques secondes, rouvrirait
    /// exactement la fenêtre que ce lot ferme (une désactivation qui met N secondes à
    /// s'appliquer). Le coût mesuré est d'une lecture par PK, négligeable devant le
    /// coût des requêtes métier que ces mêmes appels exécutent ensuite.
    /// </summary>
    public interface ISessionValidationService
    {
        /// <summary>
        /// Résultat de la validation : <c>EstValide = false</c> ⇒ 401, sans autre
        /// détail (le motif est journalisé côté serveur, jamais renvoyé).
        /// </summary>
        Task<SessionValidationResult> ValiderAsync(ClaimsPrincipal principal);
    }

    public sealed record SessionValidationResult(bool EstValide, string? Motif = null)
    {
        public static readonly SessionValidationResult Valide = new(true);
    }

    public class SessionValidationService : ISessionValidationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SessionValidationService> _logger;

        public SessionValidationService(
            UserManager<ApplicationUser> userManager,
            ILogger<SessionValidationService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<SessionValidationResult> ValiderAsync(ClaimsPrincipal principal)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? principal.FindFirstValue("UserId");

            if (string.IsNullOrEmpty(userId))
                return new SessionValidationResult(false, "Jeton sans identifiant d'utilisateur.");

            // AsNoTracking : lecture seule, aucun gain à matérialiser le suivi EF, et
            // évite de garder une entrée dans le IdentityTracker par requête.
            var stampEnBase = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.SecurityStamp, u.EstActif })
                .FirstOrDefaultAsync();

            if (stampEnBase == null)
            {
                _logger.LogWarning("Jeton refusé : le compte {UserId} n'existe plus.", userId);
                return new SessionValidationResult(false, "Compte introuvable.");
            }

            if (!stampEnBase.EstActif)
            {
                _logger.LogWarning("Jeton refusé : le compte {UserId} est désactivé.", userId);
                return new SessionValidationResult(false, "Compte désactivé.");
            }

            var stampDuJeton = principal.FindFirstValue(TokenService.SecurityStampClaim);
            if (stampDuJeton != stampEnBase.SecurityStamp)
            {
                _logger.LogWarning(
                    "Jeton refusé pour {UserId} : SecurityStamp périmé (le compte a changé depuis l'émission du jeton).",
                    userId);
                return new SessionValidationResult(false, "Session invalidée.");
            }

            return SessionValidationResult.Valide;
        }
    }
}
