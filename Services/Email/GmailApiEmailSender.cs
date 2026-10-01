using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Envoi des emails système via l'API Gmail en HTTPS, en réutilisant le module
    /// Courriels (<see cref="IGmailApiService"/>) et la connexion OAuth déjà autorisée.
    ///
    /// Pourquoi Gmail plutôt qu'un service d'emailing dédié (Resend, SMTP) :
    /// <list type="bullet">
    /// <item>l'hébergement gratuit (Render) <b>bloque les ports SMTP</b> 25/465/587, et
    /// Resend exige un domaine vérifié que l'application n'a pas ;</item>
    /// <item>l'application possède déjà, pour le module Courriels, une intégration Gmail
    /// fonctionnelle (OAuth, refresh token chiffré, construction MIME, assainissement
    /// HTML) — la réutiliser n'ajoute aucune dépendance, aucune clé, aucun domaine.</item>
    /// </list>
    ///
    /// CONFIGURATION (jamais commitée) :
    /// <list type="bullet">
    /// <item><c>Email:SenderGmailAddress</c> — variable d'environnement
    /// <c>Email__SenderGmailAddress</c>. C'est l'adresse Gmail qui doit avoir été
    /// connectée au module Courriels et rester <c>IsActive</c>. C'est sa présence qui
    /// décide de l'émetteur actif (voir <see cref="SystemEmailSender"/>).</item>
    /// </list>
    ///
    /// CONTRAT D'ÉCHEC : toute impossibilité d'envoyer (adresse système absente, aucune
    /// connexion active, refresh token révoqué, refus de l'API) lève
    /// <see cref="EmailEnvoyeException"/>. C'est le rôle de
    /// <c>PasswordSetupLinkService</c> — seul point d'entrée applicatif — de la
    /// rattraper : le flux métier qui a déclenché l'email ne doit jamais s'effondrer
    /// parce qu'un email n'est pas parti.
    /// </summary>
    public class GmailApiEmailSender : IEmailSender
    {
        private readonly ApplicationDbContext _context;
        private readonly IGmailApiService _gmailApi;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GmailApiEmailSender> _logger;

        public GmailApiEmailSender(
            ApplicationDbContext context,
            IGmailApiService gmailApi,
            IConfiguration configuration,
            ILogger<GmailApiEmailSender> logger)
        {
            _context = context;
            _gmailApi = gmailApi;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>Adresse Gmail émettrice des emails système, ou chaîne vide si non configurée.</summary>
        public string AdresseEmetteur =>
            (_configuration["Email:SenderGmailAddress"] ?? string.Empty).Trim();

        /// <summary>Vrai si une adresse système est configurée (l'émetteur peut alors être choisi).</summary>
        public bool EstConfiguree => AdresseEmetteur.Length > 0;

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var adresse = AdresseEmetteur;
            if (adresse.Length == 0)
            {
                throw new EmailEnvoyeException(
                    "Email:SenderGmailAddress n'est pas configurée : aucun compte Gmail émetteur pour les emails système.");
            }

            // Une adresse Gmail est insensible à la casse : on compare en minuscules,
            // sinon « System@Gmail.com » et « system@gmail.com » ne se reconnaîtraient pas.
            var adresseMinuscules = adresse.ToLowerInvariant();
            var connexion = await _context.GmailConnections
                .Where(c => c.IsActive)
                .FirstOrDefaultAsync(c => c.GmailAddress.ToLower() == adresseMinuscules);

            if (connexion is null)
            {
                // Le canal configuré ne peut pas émettre : on le dit explicitement (destinataire
                // et objet, jamais le corps — le corps d'un email de mot de passe porte le jeton).
                _logger.LogError(
                    "Aucune connexion Gmail active pour l'adresse système {Adresse} : " +
                    "email « {Subject} » non envoyé à {Recipient}. Reconnecter ce compte dans le module Courriels.",
                    adresse, subject, to);

                throw new EmailEnvoyeException(
                    $"Aucune connexion Gmail active pour {adresse}. Reconnectez ce compte dans le module Courriels.");
            }

            try
            {
                // Le corps HTML est assaini par le module Gmail (HtmlSanitizer) avant envoi.
                // Pas d'objet enrichi, pas de pièce jointe : ce sont des emails système.
                await _gmailApi.SendMessageAsync(
                    connexion,
                    to: to,
                    cc: null,
                    bcc: null,
                    subject: subject,
                    bodyText: null,
                    bodyHtml: htmlBody,
                    threadId: null,
                    inReplyTo: null,
                    attachments: Array.Empty<(string FileName, string MimeType, byte[] Content)>());

                _logger.LogInformation(
                    "Email « {Subject} » envoyé à {Recipient} via Gmail ({Adresse}).",
                    subject, to, adresse);
            }
            catch (Exception ex)
            {
                // Le refresh token révoqué est déjà journalisé et désactivé par GmailOAuthService ;
                // on ajoute ici le contexte du message pour rendre l'incident exploitable.
                _logger.LogError(
                    ex,
                    "Envoi via Gmail ({Adresse}) de l'email « {Subject} » à {Recipient} échoué.",
                    adresse, subject, to);

                throw new EmailEnvoyeException(
                    $"Envoi Gmail échoué ({adresse} → {to}) : {ex.Message}", ex);
            }
        }
    }
}
