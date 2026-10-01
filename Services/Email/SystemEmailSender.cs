namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Sélection de l'émetteur des emails système, résolue à chaque envoi.
    ///
    /// Deux cas, et deux seulement :
    /// <list type="bullet">
    /// <item><c>Email:SenderGmailAddress</c> est renseignée → <see cref="GmailApiEmailSender"/>.
    /// L'envoi part par la vraie boîte Gmail ; un échec (connexion absente/inactive,
    /// refresh token révoqué, refus de l'API) lève <see cref="EmailEnvoyeException"/>,
    /// rattrapée par <c>PasswordSetupLinkService</c>.</item>
    /// <item>L'adresse est absente → <see cref="LoggingEmailSender"/>. C'est le cas d'un
    /// poste de dev, de la suite de tests ou d'un environnement de recette : l'API
    /// démarre, le lien est lisible dans les logs hors Production, et aucun appel
    /// réseau n'est tenté.</item>
    /// </list>
    ///
    /// La décision est prise à CHAQUE envoi (et non figée au démarrage) : c'est ce qui
    /// permet de connecter l'adresse Gmail après le lancement du processus sans
    /// redéployer, et à l'inverse de retomber sur la journalisation si la
    /// configuration change.
    /// </summary>
    public class SystemEmailSender : IEmailSender
    {
        private readonly GmailApiEmailSender _gmail;
        private readonly LoggingEmailSender _logging;
        private readonly ILogger<SystemEmailSender> _logger;

        public SystemEmailSender(
            GmailApiEmailSender gmail,
            LoggingEmailSender logging,
            ILogger<SystemEmailSender> logger)
        {
            _gmail = gmail;
            _logging = logging;
            _logger = logger;
        }

        public Task SendAsync(string to, string subject, string htmlBody)
        {
            if (_gmail.EstConfiguree)
            {
                return _gmail.SendAsync(to, subject, htmlBody);
            }

            _logger.LogWarning(
                "Email:SenderGmailAddress non configurée : l'email « {Subject} » destiné à {Recipient} " +
                "est remis à l'émetteur journalisé au lieu d'être envoyé par Gmail.",
                subject, to);

            return _logging.SendAsync(to, subject, htmlBody);
        }
    }
}
