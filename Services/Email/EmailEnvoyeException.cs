using System.Net;

namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Échec d'envoi d'un email transactionnel.
    /// </summary>
    public class EmailEnvoyeException : Exception
    {
        public EmailEnvoyeException(string message) : base(message)
        {
        }

        public EmailEnvoyeException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
