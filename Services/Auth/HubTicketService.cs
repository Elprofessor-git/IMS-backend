using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Backend_Gestion_Magasin_API.Services.Auth
{
    /// <summary>
    /// Ticket éphémère d'authentification du hub Discussion.
    ///
    /// Un ticket est un JETON OPAQUE aléatoire de 256 bits, émis par un endpoint REST
    /// protégé (<c>POST /api/Discussion/hub-ticket</c>) et valable UNIQUEMENT pour la
    /// connexion au hub :
    /// <list type="bullet">
    /// <item><b>à usage unique</b> : la première validation le retire du magasin
    /// (TryRemove atomique), une seconde présentation échoue ;</item>
    /// <item><b>60 secondes de vie</b> : au-delà, il est refusé et purgé ;</li>
    /// <item><b>lié au SecurityStamp</b> du compte au moment de l'émission : un mot de
    /// passe changé ou un compte désactivé entre l'émission et la connexion invalide le
    /// ticket, sans avoir à maintenir une liste de révocation.</item>
    /// </list>
    ///
    /// Volontairement SANS lien avec les JWT métier : le ticket est un jeton OPAQUE
    /// aléatoire, jamais signé, donc AUCUNE route REST (qui valide des JWT signés) ne
    /// peut l'accepter. C'est ce qui garantit « le ticket n'est valable que pour le
    /// hub, et refusé par toutes les routes REST ».
    ///
    /// Le magasin est un singleton en mémoire : un ticket est à usage unique et vit
    /// au plus 60 secondes, une persistance serait inutile et coûteuse. Sur plusieurs
    /// instances, il faudrait un cache partagé — hors périmètre ici (Render mono-instance).
    /// </summary>
    public interface IHubTicketService
    {
        /// <summary>Émet un ticket à usage unique, valable 60 secondes.</summary>
        string Creer(string userId, string? securityStamp);

        /// <summary>
        /// Consomme un ticket. Renvoie ses informations la PREMIÈRE fois, puis
        /// <c>null</c> (déjà utilisé, inconnu ou expiré).
        /// </summary>
        HubTicket? Consommer(string ticket);
    }

    /// <summary>Contenu d'un ticket : à qui il lie la connexion et jusqu'à quand.</summary>
    public sealed record HubTicket(string UserId, string? SecurityStamp, DateTimeOffset ExpireLe);

    public sealed class HubTicketService : IHubTicketService
    {
        /// <summary>Durée de vie d'un ticket : 60 secondes, non configurable.</summary>
        public static readonly TimeSpan Duree = TimeSpan.FromSeconds(60);

        private readonly ConcurrentDictionary<string, HubTicket> _tickets = new();
        private readonly ILogger<HubTicketService> _logger;
        private readonly TimeProvider _temps;

        public HubTicketService(ILogger<HubTicketService> logger, TimeProvider temps)
        {
            _logger = logger;
            _temps = temps;
        }

        public string Creer(string userId, string? securityStamp)
        {
            Nettoyer();

            var ticket = Generer();
            _tickets[ticket] = new HubTicket(userId, securityStamp, _temps.GetUtcNow() + Duree);

            _logger.LogDebug("Ticket de hub émis pour {UserId}.", userId);
            return ticket;
        }

        public HubTicket? Consommer(string ticket)
        {
            // TryRemove est ATOMIQUE : deux connexions concurrentes présentant le même
            // ticket ne peuvent pas l'emporter toutes les deux. La seconde obtient null.
            if (!_tickets.TryRemove(ticket, out var info))
                return null;

            if (info.ExpireLe <= _temps.GetUtcNow())
            {
                _logger.LogWarning("Ticket de hub expiré pour {UserId}.", info.UserId);
                return null;
            }

            return info;
        }

        /// <summary>Jeton opaque aléatoire (256 bits, base64url) : jamais signé, jamais devinable.</summary>
        private static string Generer()
        {
            var octets = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(octets)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>Purge paresseuse : appelée à chaque émission, bornée par le faible volume.</summary>
        private void Nettoyer()
        {
            var maintenant = _temps.GetUtcNow();
            foreach (var (cle, info) in _tickets)
            {
                if (info.ExpireLe <= maintenant)
                    _tickets.TryRemove(cle, out _);
            }
        }
    }
}
