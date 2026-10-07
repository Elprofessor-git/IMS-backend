using Microsoft.Extensions.Options;

namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Sélection de l'émetteur des emails système, résolue à chaque envoi.
    ///
    /// Trois cas, et trois seulement, dans cet ordre :
    /// <list type="number">
    /// <item><c>Smtp__Host</c> renseigné → <see cref="SmtpEmailSender"/>. Envoi par
    /// SMTP (mot de passe d'application Gmail), sans écran de consentement ni refresh
    /// token. Un échec lève <see cref="EmailEnvoyeException"/>.</item>
    /// <item>sinon <c>Email:SenderGmailAddress</c> renseignée → <see cref="GmailApiEmailSender"/>.
    /// C'est l'implémentation historique : l'envoi part par l'API Gmail en HTTPS en
    /// réutilisant la connexion OAuth du module Courriels.</item>
    /// <item>sinon → <see cref="LoggingEmailSender"/>. Poste de dev, suite de tests,
    /// recette : l'API démarre, le lien est lisible dans les logs hors Production,
    /// aucun appel réseau n'est tenté.</item>
    /// </list>
    ///
    /// Pourquoi SMTP prime : l'API Gmail impose un écran de consentement, un
    /// branding et un refresh token expirant en mode Test. SMTP ne demande qu'un
    /// mot de passe d'application. Mais Render bloque les ports 25/465/587 sur les
    /// instances gratuites — le mode est donc une OPTION de configuration, pas un
    /// remplacement : l'opérateur qui ne dispose pas de port ouvert ne pose pas
    /// <c>Smtp__Host</c> et garde exactement le comportement précédent.
    ///
    /// La décision est prise à CHAQUE envoi (et non figée au démarrage) : c'est ce
    /// qui permet de changer de canal sans redéployer, et de retomber sur la
    /// journalisation si la configuration change. Le contrôle des clés obligatoires,
    /// lui, est fait une fois au démarrage par
    /// <see cref="VerificationDemarrageSmtp"/>.
    /// </summary>
    public class SystemEmailSender : IEmailSender
    {
        private readonly SmtpEmailSender _smtp;
        private readonly GmailApiEmailSender _gmail;
        private readonly LoggingEmailSender _logging;
        private readonly IOptions<SmtpOptions> _smtpOptions;
        private readonly ILogger<SystemEmailSender> _logger;

        public SystemEmailSender(
            SmtpEmailSender smtp,
            GmailApiEmailSender gmail,
            LoggingEmailSender logging,
            IOptions<SmtpOptions> smtpOptions,
            ILogger<SystemEmailSender> logger)
        {
            _smtp = smtp;
            _gmail = gmail;
            _logging = logging;
            _smtpOptions = smtpOptions;
            _logger = logger;
        }

        public Task SendAsync(string to, string subject, string htmlBody)
        {
            // Ordre strictement celui de la configuration : Smtp__Host -> SMTP,
            // sinon Email:SenderGmailAddress -> API Gmail, sinon journalisation.
            if (_smtpOptions.Value.EstModeSmtp)
            {
                return _smtp.SendAsync(to, subject, htmlBody);
            }

            if (_gmail.EstConfiguree)
            {
                return _gmail.SendAsync(to, subject, htmlBody);
            }

            _logger.LogWarning(
                "Aucun émetteur configuré (Smtp__Host et Email__SenderGmailAddress absents) : " +
                "l'email « {Subject} » destiné à {Recipient} est remis à l'émetteur journalisé.",
                subject, to);

            return _logging.SendAsync(to, subject, htmlBody);
        }
    }
}
