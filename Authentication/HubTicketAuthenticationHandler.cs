using System.Security.Claims;
using System.Text.Encodings.Web;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backend_Gestion_Magasin_API.Authentication
{
    /// <summary>Options du schéma d'authentification par ticket de hub (aucune).</summary>
    public sealed class HubTicketAuthenticationOptions : AuthenticationSchemeOptions
    {
    }

    /// <summary>
    /// Schéma d'authentification <b>HubTicket</b>, réservé au hub Discussion (
    /// <c>[Authorize(AuthenticationSchemes = "HubTicket")]</c>).
    ///
    /// Le jeton est lu dans la query string <c>access_token</c> (obligatoire pour les
    /// WebSockets, où le navigateur ne peut poser d'en-tête) ou, à défaut, dans l'en-tête
    /// <c>Authorization: Bearer</c>. C'est un TICKET OPAQUE à usage unique, jamais un JWT :
    /// le JWT principal reste dans le cookie httpOnly du frontend et n'apparaît JAMAIS
    /// dans une URL.
    ///
    /// À chaque validation, le ticket est CONSOMMÉ (usage unique), puis le compte est
    /// relu en base :
    /// <list type="bullet">
    /// <item>compte absent, désactivé (<see cref="ApplicationUser.EstActif"/>) ou non
    /// interne → refus immédiat ;</item>
    /// <item>SecurityStamp différent de celui figé au moment de l'émission (mot de passe
    /// changé/réinitialisé, rôle modifié) → session invalidée.</item>
    /// </list>
    /// Un compte désactivé après l'émission du ticket mais avant la connexion est donc
    /// refusé, sans attendre l'expiration du ticket.
    /// </summary>
    public sealed class HubTicketAuthenticationHandler
        : AuthenticationHandler<HubTicketAuthenticationOptions>
    {
        /// <summary>Nom du schéma, partagé avec le hub et l'enregistrement DI.</summary>
        public const string SchemeName = "HubTicket";

        private readonly IHubTicketService _tickets;
        private readonly UserManager<ApplicationUser> _users;

        public HubTicketAuthenticationHandler(
            IOptionsMonitor<HubTicketAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IHubTicketService tickets,
            UserManager<ApplicationUser> users)
            : base(options, logger, encoder)
        {
            _tickets = tickets;
            _users = users;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var ticket = LireTicket(Request);
            if (string.IsNullOrEmpty(ticket))
                return AuthenticateResult.NoResult();

            var info = _tickets.Consommer(ticket);
            if (info == null)
                return AuthenticateResult.Fail("Ticket de hub invalide, déjà utilisé ou expiré.");

            var user = await _users.FindByIdAsync(info.UserId);
            if (user == null)
                return AuthenticateResult.Fail("Ticket de hub : compte introuvable.");

            if (!user.EstActif || !user.EstCompteInterne)
                return AuthenticateResult.Fail("Ticket de hub : compte désactivé ou non interne.");

            if (!string.Equals(user.SecurityStamp, info.SecurityStamp, StringComparison.Ordinal))
                return AuthenticateResult.Fail("Ticket de hub : SecurityStamp périmé, session invalidée.");

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
            if (!string.IsNullOrEmpty(user.UserName))
                identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));

            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }

        /// <summary>
        /// Ticket lu dans la query string (obligatoire pour les WebSockets du
        /// navigateur, qui ne peuvent pas poser d'en-tête) ou, à défaut, dans
        /// l'en-tête <c>Authorization: Bearer</c>.
        /// </summary>
        private static string? LireTicket(HttpRequest request)
        {
            var query = request.Query["access_token"].ToString();
            if (!string.IsNullOrEmpty(query))
                return query;

            var entete = request.Headers.Authorization.ToString();
            if (entete.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return entete["Bearer ".Length..].Trim();

            return null;
        }
    }
}
