using System.Security.Claims;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Identité de l'utilisateur courant, dérivée EXCLUSIVEMENT des claims du JWT.
    ///
    /// Règle d'or du module Tâches : le frontend n'est jamais une source fiable
    /// d'identité. Aucun contrôleur ne doit accepter un UserId provenant du corps
    /// de la requête ou d'un query string pour déterminer un propriétaire ; seul
    /// <see cref="UserId"/> fait foi.
    ///
    /// Les DROITS (voir toutes les tâches, assigner) ne sont pas porté par ce service :
    /// ils vivent dans la base (table Role) et sont résolus par IPermissionService.
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>Identifiant système (AspNetUsers.Id) porté par le claim NameIdentifier.</summary>
        string? UserId { get; }

        /// <summary>Nom d'affichage, ou à défaut l'identifiant.</summary>
        string? UserName { get; }

        bool IsAuthenticated { get; }
    }

    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public string? UserId =>
            Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Principal?.FindFirstValue("UserId");

        public string? UserName => Principal?.Identity?.Name ?? UserId;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    }
}
