using Microsoft.Extensions.Options;

namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Envoi des emails système par SMTP (MailKit), derrière
    /// <see cref="IEmailSender"/> — même contrat que l'implémentation Gmail :
    /// l'échec lève <see cref="EmailEnvoyeException"/>, rattrapée par
    /// <c>PasswordSetupLinkService</c> pour que le flux métier ne s'effondre pas.
    ///
    /// CONFIGURATION (jamais commitée) : section <c>Smtp</c> → variables
    /// <c>Smtp__Host</c>, <c>Smtp__Port</c>, <c>Smtp__User</c>, <c>Smtp__Password</c>,
    /// <c>Smtp__From</c>, <c>Smtp__FromName</c>. Le mode SMTP n'est retenu que si
    /// <c>Smtp__Host</c> est présent ; voir <see cref="SystemEmailSender"/>.
    ///
    /// JOURNALISATION : destinataire et objet uniquement. Le corps n'apparaît
    /// jamais — il porte le jeton de réinitialisation, dont la lecture équivaut à
    /// la prise de contrôle du compte. Le mot de passe n'apparaît jamais non plus.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        /// <summary>
        /// Message d'incidents le plus utile à produire : quand SMTP échoue par
        /// timeout sur un hébergeur, la cause la plus probable est un port bloqué.
        /// </summary>
        public const string MessageInjoignable =
            "SMTP injoignable : ports bloqués ? (Render gratuit bloque 25/465/587)";

        private readonly IOptions<SmtpOptions> _options;
        private readonly ISmtpTransport _transport;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(
            IOptions<SmtpOptions> options,
            ISmtpTransport transport,
            ILogger<SmtpEmailSender> logger)
        {
            _options = options;
            _transport = transport;
            _logger = logger;
        }

        /// <summary>Adresse d'expéditeur configurée, ou chaîne vide. Ne contient jamais le mot de passe.</summary>
        public string AdresseEmetteur => _options.Value.From.Trim();

        /// <summary>Vrai si la section Smtp est complète : c'est ce qui autorise le choix SMTP dans le sélecteur.</summary>
        public bool EstConfigure => _options.Value.EstComplete;

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var options = _options.Value;
            var manquantes = options.ChampsManquants();
            if (manquantes.Count > 0)
            {
                throw new EmailEnvoyeException(
                    "Configuration SMTP incomplète (clés absentes : " + string.Join(", ", manquantes) +
                    ") : l'email n'a pas été envoyé.");
            }

            try
            {
                await _transport.EnvoyerAsync(options, to, subject, htmlBody);
            }
            catch (Exception ex)
            {
                // Ni corps ni lien dans l'exception ni dans le journal : le message
                // d'erreur part dans des agrégateurs, avec une rétention bien plus
                // longue que la durée de vie du jeton.
                if (EstTimeout(ex))
                {
                    _logger.LogError(
                        ex,
                        "{Message} — hôte {Host}:{Port}. L'email « {Subject} » destiné à {Recipient} " +
                        "n'a pas été envoyé.",
                        MessageInjoignable, options.Host, options.Port, subject, to);
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Envoi SMTP ({Host}:{Port}) de l'email « {Subject} » à {Recipient} échoué.",
                        options.Host, options.Port, subject, to);
                }

                throw new EmailEnvoyeException(
                    $"Envoi SMTP échoué ({options.Host}:{options.Port} → {to}) : {ex.Message}", ex);
            }

            _logger.LogInformation(
                "Email « {Subject} » envoyé à {Recipient} via SMTP ({Host}:{Port}).",
                subject, to, options.Host, options.Port);
        }

        /// <summary>
        /// Détecte un timeout dans toute la chaîne d'exception. MailKit lève tantôt
        /// <c>OperationCanceledException</c>, tantôt une <c>IOException</c> « operation
        /// has timed out » — selon que c'est notre timeout ou celui du socket qui gagne.
        /// </summary>
        private static bool EstTimeout(Exception ex)
        {
            for (var cause = ex; cause is not null; cause = cause.InnerException)
            {
                if (cause is OperationCanceledException) return true;

                var message = cause.Message;
                if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("time out", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
