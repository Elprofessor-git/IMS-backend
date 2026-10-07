namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Transport SMTP réellement distinct de l'émetteur, pour que les tests ne
    /// branchent AUCUNE connexion réseau : <c>SmtpEmailSender</c> teste la logique
    /// (options, journalisation, traduction d'échec) sur un double en mémoire, et
    /// <see cref="MailKitSmtpTransport"/> reste le seul code qui ouvre un socket.
    /// </summary>
    public interface ISmtpTransport
    {
        /// <summary>
        /// Envoie un email. Lève une exception en cas d'échec ; ne journalise rien.
        /// </summary>
        Task EnvoyerAsync(SmtpOptions options, string to, string subject, string htmlBody);
    }
}
