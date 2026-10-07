using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Transport SMTP via MailKit. SEULE partie du chemin d'email qui ouvre une
    /// connexion réseau : les tests ne la couvrent pas, ils branchent un double de
    /// <see cref="ISmtpTransport"/>.
    ///
    /// Sécurité et robustesse :
    /// <list type="bullet">
    /// <item>STARTTLS <b>obligatoire</b> : <see cref="SecureSocketOptions.StartTls"/> échoue
    /// plutôt que de tomber en clair si le serveur n'offre pas l'extension ;</item>
    /// <item>timeout explicite de <c>TimeoutEnvoiMilliseconds</c> (10 s) sur toute l'opération :
    /// sans lui, MailKit attend indéfiniment et la requête HTTP qui a déclenché l'email
    /// reste ouverte jusqu'au timeout du reverse-proxy ;</item>
    /// <item>aucun corps, aucun lien ni mot de passe ne sont journalisés ici — cette
    /// classe ne journalise pas du tout, elle lève.</item>
    /// </list>
    /// </summary>
    public sealed class MailKitSmtpTransport : ISmtpTransport
    {
        /// <summary>Timeout global d'envoi, en millisecondes (10 s).</summary>
        public const int TimeoutEnvoiMilliseconds = 10_000;

        public async Task EnvoyerAsync(SmtpOptions options, string to, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.From));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient
            {
                Timeout = TimeoutEnvoiMilliseconds,
            };

            // Port 587 : soumission avec démarrage explicite de STARTTLS.
            await client.ConnectAsync(options.Host, options.Port, SecureSocketOptions.StartTls);
            try
            {
                await client.AuthenticateAsync(options.User, options.Password);
                await client.SendAsync(message);
            }
            finally
            {
                // Déconnexion best-effort : une exception ici ne doit pas masquer
                // la cause réelle (auth refusée, expéditeur non vérifié…).
                await client.DisconnectAsync(quit: true);
            }
        }
    }
}
