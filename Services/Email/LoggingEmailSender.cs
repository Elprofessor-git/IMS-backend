namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Émetteur de substitution : journalise l'email au lieu de l'envoyer.
    ///
    /// C'est l'implémentation ACTIVE dès qu'aucune clé Resend n'est configurée
    /// (poste de dev, suite de tests d'intégration, environnement de recette sans
    /// accès à Resend). Objectif : aucun test ne doit dépendre d'un envoi réseau
    /// réel, et l'API doit rester utilisable — un email non parti est un incident,
    /// pas une raison de ne pas démarrer.
    ///
    /// Le CORPS n'est journalisé qu'hors Production, et c'est la partie qui compte :
    /// un corps d'email de mot de passe contient le jeton, donc la capacité à
    /// réinitialiser le mot de passe de n'importe quel compte tant que le jeton
    /// vit. Journaliser cela en production installerait, dans chaque agrégateur de
    /// logs et pour toute la durée de leur rétention, une clé de prise de contrôle
    /// des comptes — l'information la plus sensible de l'application. En Production
    /// on ne garde donc que destinataire et objet : de quoi diagnostiquer un envoi
    /// non parti, pas de quoi réinitialiser un mot de passe.
    ///
    /// SecurityStamp : jamais journalisé, nulle part, nulle part.
    /// </summary>
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;
        private readonly IHostEnvironment _environment;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public Task SendAsync(string to, string subject, string htmlBody)
        {
            if (_environment.IsProduction())
            {
                // Ni corps, ni lien : en production, cet émetteur signale une
                // configuration manquante, il n'est pas un outil de débogage.
                _logger.LogError(
                    "EMAIL NON ENVOYÉ (aucun émetteur configuré en production — RESEND_API_KEY absente). " +
                    "Destinataire : {Recipient}. Objet : {Subject}. Le destinataire ne recevra rien.",
                    to, subject);
            }
            else
            {
                // Hors production : le corps entier est journalisé, ce qui permet de
                // lire un lien de réinitialisation sans capture d'email.
                _logger.LogInformation(
                    "EMAIL (non envoyé — aucun émetteur configuré)\n Destinataire : {Recipient}\n Objet : {Subject}\n Corps :\n{Body}",
                    to, subject, htmlBody);
            }

            return Task.CompletedTask;
        }
    }
}