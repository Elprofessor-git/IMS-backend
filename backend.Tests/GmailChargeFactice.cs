using System.Net;
using System.Text;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Charge utile Gmail fabriquée, servie au VRAI <see cref="GmailApiService"/> par un
/// gestionnaire HTTP local : aucun appel ne sort, mais le parcours MIME complet est
/// exercé (en-têtes <c>Content-Disposition</c>, remontée des parties imbriquées,
/// comparaison des <c>cid:</c>).
/// <para>
/// Partagé par les tests de qualification (A2) et par ceux du rattrapage (A2b) : le
/// double enregistreur, lui, court-circuite le service et sert des pièces DÉJÀ
/// qualifiées — il ne peut donc pas prouver d'où vient la qualification.
/// </para>
/// </summary>
internal static class GmailChargeFactice
{
    private sealed class GestionnaireCharge : HttpMessageHandler
    {
        private readonly string _json;
        public List<string> Urls { get; } = new();

        public GestionnaireCharge(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class OAuthFactice : IGmailOAuthService
    {
        public bool IsConfigured => true;
        public string BuildAuthorizationUrl(string userId) => "https://exemple.test/auth";
        public Task<GmailConnection> HandleCallbackAsync(string code, string state) =>
            throw new NotSupportedException();
        public string BuildPostCallbackRedirectUrl(bool success, string? errorMessage = null) =>
            "https://ims.test";
        public Task RevokeAsync(GmailConnection connection) => Task.CompletedTask;
        public Task<string> GetFreshAccessTokenAsync(GmailConnection connection) =>
            Task.FromResult("jeton-de-test");
    }

    private sealed class ClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public ClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static GmailConnection Connexion() => new()
    {
        UserId = "u1",
        GmailAddress = "moi@ims.test",
        GoogleUserId = "google-1",
        RefreshTokenEncrypted = "chiffre",
        GrantedScopes = "gmail.readonly"
    };

    /// <summary>
    /// Lit un message fabriqué et renvoie le résultat du vrai service, avec les URL
    /// appelées (une seule attendue : le test doit prouver qu'aucun aller-retour
    /// supplémentaire n'est nécessaire).
    /// </summary>
    public static (GmailApiMessage Message, List<string> Urls) Lire(string json, string id = "msg-1")
    {
        var handler = new GestionnaireCharge(json);
        var service = new GmailApiService(
            new OAuthFactice(), new ClientFactory(handler), NullLogger<GmailApiService>.Instance);

        return (service.GetMessageAsync(Connexion(), id).GetAwaiter().GetResult(), handler.Urls);
    }

    // ── Fabrication du JSON ─────────────────────────────────────────────────

    public static string EnTete(string nom, string valeur) =>
        $$"""{"name": "{{nom}}", "value": "{{valeur}}"}""";

    /// <summary>
    /// Partie MIME avec ses en-têtes réels. <paramref name="disposition"/> est LA valeur
    /// lue par le service : « attachment » produit
    /// <c>Content-Disposition: attachment; filename="…"</c>, « inline » produit
    /// <c>Content-Disposition: inline</c>. Les guillemets du nom de fichier sont
    /// échappés pour le JSON, sans quoi la charge utile devient illisible.
    /// </summary>
    public static string Part(
        string partId, string mime, string disposition, string? cid, string filename, int size = 1024)
    {
        var dispositionValue = disposition == "attachment"
            ? $"attachment; filename=\\\"{filename}\\\""
            : "inline";
        var cidHeader = cid == null ? "" : EnTete("Content-ID", $"<{cid}>") + ", ";

        return $$"""
            {
              "partId": "{{partId}}",
              "mimeType": "{{mime}}",
              "filename": "{{filename}}",
              "headers": [{{cidHeader}}{{EnTete("Content-Disposition", dispositionValue)}}, {{EnTete("Content-Type", mime)}}],
              "body": { "size": {{size}}, "attachmentId": "att-{{partId}}" }
            }
            """;
    }

    /// <summary>Message multipart/mixed : corps texte, corps HTML, puis les pièces.</summary>
    public static string Message(string html, params string[] parts)
    {
        var partList = string.Join(",", parts);
        // Gmail renvoie le corps en base64 dans body.data : sans lui, le service ne trouve
        // aucun HTML et n'aurait aucun cid: à examiner.
        var htmlData = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));

        return $$"""
            {
              "id": "msg-1",
              "threadId": "thread-1",
              "labelIds": ["INBOX"],
              "snippet": "extrait",
              "internalDate": "1767225600000",
              "payload": {
                "partId": "",
                "mimeType": "multipart/mixed",
                "headers": [{{EnTete("Message-Id", "<abc@exemple.fr>")}}, {{EnTete("Subject", "Bonjour")}}, {{EnTete("From", "client@exemple.fr")}}],
                "body": { "size": 0 },
                "parts": [
                  {
                    "partId": "0",
                    "mimeType": "text/plain",
                    "headers": [{{EnTete("Content-Type", "text/plain; charset=UTF-8")}}],
                    "body": { "size": 10, "data": "{{Convert.ToBase64String(Encoding.UTF8.GetBytes("Bonjour"))}}" }
                  },
                  {
                    "partId": "1",
                    "mimeType": "text/html",
                    "headers": [{{EnTete("Content-Type", "text/html; charset=UTF-8")}}],
                    "body": { "size": {{html.Length}}, "data": "{{htmlData}}" }
                  }{{(partList.Length > 0 ? "," + partList : "")}}
                ]
              }
            }
            """;
    }
}
