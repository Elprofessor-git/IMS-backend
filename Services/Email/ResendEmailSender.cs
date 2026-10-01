using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Envoi via l'API REST Resend (https://api.resend.com/emails).
    ///
    /// Aucun SDK Resend n'est ajouté : l'appel est fait avec un <see cref="HttpClient"/>
    /// nommé, exactement comme le module Courriels appelle l'API Google
    /// (<see cref="Services.Gmail.GmailApiService"/>) et l'assistant IA l'API Groq
    /// (<see cref="Services.GroqService"/>). Une dépendance de plus pour trois
    /// propriétés JSON n'aurait rien apporté, et le projet n'embarque volontairement
    /// que les paquets dont il a l'usage.
    ///
    /// CONFIGURATION (jamais dans appsettings.json versionné, jamais commitée) :
    /// <list type="bullet">
    /// <item><c>RESEND_API_KEY</c> — variable d'environnement, lue au premier
    /// démarrage. C'est sa présence qui décide de l'implémentation active
    /// (<see cref="LoggingEmailSender"/> sinon) : un poste de dev ou une suite de tests
    /// sans clé ne fait donc JAMAIS d'appel réseau.</item>
    /// <item><c>Resend:FromAddress</c> — expéditeur, lisible par variable
    /// d'environnement (<c>Resend__FromAddress</c>). Doit être un domaine vérifié
    /// chez Resend ; sinon l'API répond 403 et l'échec est journalisé (voir
    /// <see cref="SendAsync"/>), sans faire échouer le flux appelant.</item>
    /// </list>
    /// </summary>
    public class ResendEmailSender : IEmailSender
    {
        public const string HttpClientName = "resend";

        /// <summary>
        /// Expéditeur par défaut. Volontairement sur le domaine de Réserve IANA
        /// « invalid » : en l'absence de configuration explicite, l'envoi échoue
        /// proprement côté Resend plutôt que de faire partir un email au nom d'un
        /// domaine qui n'appartient pas à l'application. Aucune adresse « plausible »
        /// n'est devinée ici — elle doit être fournie par la configuration.
        /// </summary>
        private const string AdresseExpéditeurParDéfaut = "IMS <no-reply@invalid.example>";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ResendEmailSender> _logger;
        private readonly string _apiKey;
        private readonly string _fromAddress;

        public ResendEmailSender(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ResendEmailSender> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // Program.cs a déjàThrowé si la clé est absente ET a choisi cette
            // implémentation : la relecture est donc un garde-fou, pas le contrôle
            // principal.
            _apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")
                      ?? configuration["Resend:ApiKey"]
                      ?? string.Empty;

            _fromAddress = configuration["Resend:FromAddress"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_fromAddress))
            {
                _fromAddress = AdresseExpéditeurParDéfaut;
                _logger.LogWarning(
                    "Resend:FromAddress absent de la configuration : envoi avec l'expéditeur par défaut {From}. " +
                    "Renseigner Resend__FromAddress avec un domaine vérifié chez Resend pour des emails réels.",
                    _fromAddress);
            }
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new ResendEmailRequest
            {
                From = _fromAddress,
                To = new[] { to },
                Subject = subject,
                Html = htmlBody
            });

            using var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email « {Subject} » envoyé à {Recipient} via Resend.", subject, to);
                return;
            }

            // Le corps de l'erreur Resend est la seule source exploitable ici : c'est
            // lui qui distingue « domaine non vérifié » d'une clé invalide ou d'un
            // destinataire refusé. On le journalise AU NIVEAU ALERTE et on remonte une
            // exception porteuse de ce diagnostic, que l'appelant rattrape.
            var corps = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Envoi Resend refusé ({StatusCode}) pour le destinataire {Recipient} : {Corps}",
                (int)response.StatusCode, to, corps);

            throw new EmailEnvoyeException(
                $"Resend a refusé l'envoi ({(int)response.StatusCode}) : {corps}");
        }

        private sealed class ResendEmailRequest
        {
            [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
            [JsonPropertyName("to")] public string[] To { get; set; } = Array.Empty<string>();
            [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
            [JsonPropertyName("html")] public string Html { get; set; } = string.Empty;
        }
    }
}
