using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend_Gestion_Magasin_API.Models.Gmail;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    public record GmailApiMessage(
        string Id,
        string ThreadId,
        string? From,
        string? To,
        string? Cc,
        string? Subject,
        string? Rfc822MessageId,
        string? BodyText,
        string? Snippet,
        DateTime ReceivedAt,
        bool IsRead,
        bool IsStarred,
        bool HasAttachments,
        List<string> LabelIds);

    public interface IGmailApiService
    {
        Task<List<string>> ListMessageIdsAsync(GmailConnection connection, string? query, int maxResults = 30, string? pageToken = null);
        Task<(List<string> Ids, string? NextPageToken)> ListMessageIdsPageAsync(GmailConnection connection, string? query, int maxResults = 30, string? pageToken = null);
        Task<GmailApiMessage> GetMessageAsync(GmailConnection connection, string gmailMessageId);
        Task<string> CreateDraftAsync(GmailConnection connection, string to, string subject, string body, string? threadId, string? inReplyToRfc822MessageId);
        Task<string> SendDraftAsync(GmailConnection connection, string draftId);
    }

    public class GmailApiService : IGmailApiService
    {
        private readonly IGmailOAuthService _oauth;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GmailApiService> _logger;
        private const string ApiBase = "https://gmail.googleapis.com/gmail/v1/users/me";

        public GmailApiService(
            IGmailOAuthService oauth,
            IHttpClientFactory httpClientFactory,
            ILogger<GmailApiService> logger)
        {
            _oauth = oauth;
            _httpClient = httpClientFactory.CreateClient("gmail");
            _logger = logger;
        }

        // L'en-tête d'autorisation est posé sur CHAQUE requête (et non sur le HttpClient partagé) :
        // un DefaultRequestHeaders muté serait partagé entre requêtes concurrentes et pourrait
        // renvoyer le token d'un autre compte sur la requête en cours.
        private async Task<HttpRequestMessage> CreateRequestAsync(
            HttpMethod method, string url, GmailConnection connection, object? jsonBody = null)
        {
            var accessToken = await _oauth.GetFreshAccessTokenAsync(connection);
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (jsonBody != null)
                request.Content = JsonContent.Create(jsonBody);
            return request;
        }

        private static async Task<string> ReadBodyAsync(HttpResponseMessage response, string context)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Erreur Gmail API sur {context} ({response.StatusCode}) : {Truncate(body, 400)}");
            return body;
        }

        public async Task<List<string>> ListMessageIdsAsync(GmailConnection connection, string? query, int maxResults = 30, string? pageToken = null)
        {
            var (ids, _) = await ListMessageIdsPageAsync(connection, query, maxResults, pageToken);
            return ids;
        }

        public async Task<(List<string> Ids, string? NextPageToken)> ListMessageIdsPageAsync(
            GmailConnection connection, string? query, int maxResults = 30, string? pageToken = null)
        {
            var q = string.IsNullOrWhiteSpace(query) ? "in:inbox" : query;
            var url = $"{ApiBase}/messages?maxResults={maxResults}&q={Uri.EscapeDataString(q)}";
            if (!string.IsNullOrWhiteSpace(pageToken))
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

            using var request = await CreateRequestAsync(HttpMethod.Get, url, connection);
            using var response = await _httpClient.SendAsync(request);
            var body = await ReadBodyAsync(response, "messages.list");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var ids = new List<string>();
            if (root.TryGetProperty("messages", out var messages))
            {
                foreach (var m in messages.EnumerateArray())
                {
                    var id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
                }
            }

            var next = root.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
            return (ids, next);
        }

        public async Task<GmailApiMessage> GetMessageAsync(GmailConnection connection, string gmailMessageId)
        {
            using var request = await CreateRequestAsync(HttpMethod.Get, $"{ApiBase}/messages/{gmailMessageId}?format=full", connection);
            using var response = await _httpClient.SendAsync(request);
            var body = await ReadBodyAsync(response, $"messages.get({gmailMessageId})");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var payload = root.GetProperty("payload");
            var headers = payload.GetProperty("headers");

            string? GetHeader(string name)
            {
                foreach (var h in headers.EnumerateArray())
                {
                    if (string.Equals(h.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase)
                        && h.TryGetProperty("value", out var v))
                        return v.GetString();
                }
                return null;
            }

            var labelIds = new List<string>();
            if (root.TryGetProperty("labelIds", out var labels))
                labelIds = labels.EnumerateArray().Select(l => l.GetString()!).ToList();

            // internalDate est un timestamp epoch en ms (chaîne). Repli sur DateHeader si absent.
            DateTime receivedAt;
            var internalDate = root.TryGetProperty("internalDate", out var idEl) ? idEl.GetString() : null;
            if (!string.IsNullOrEmpty(internalDate) && long.TryParse(internalDate, out var ms))
            {
                receivedAt = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            }
            else if (DateTime.TryParse(GetHeader("Date"), out var parsed))
            {
                receivedAt = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }
            else
            {
                receivedAt = DateTime.UtcNow;
            }

            // « Message-Id » peut arriver avec ou sans chevrons : on normalise sans chevrons,
            // le service de composition les rajoute.
            var rfc822 = GetHeader("Message-Id")?.Trim().Trim('<', '>');

            var gmailId = root.GetProperty("id").GetString()!;
            var threadId = root.TryGetProperty("threadId", out var tEl) && !string.IsNullOrEmpty(tEl.GetString())
                ? tEl.GetString()!
                : gmailId;

            return new GmailApiMessage(
                Id: gmailId,
                ThreadId: threadId,
                From: GetHeader("From"),
                To: GetHeader("To"),
                Cc: GetHeader("Cc"),
                Subject: GetHeader("Subject"),
                Rfc822MessageId: string.IsNullOrEmpty(rfc822) ? null : rfc822,
                BodyText: ExtractPlainTextBody(payload),
                Snippet: root.TryGetProperty("snippet", out var sn) ? sn.GetString() : null,
                ReceivedAt: receivedAt,
                IsRead: !labelIds.Contains("UNREAD"),
                IsStarred: labelIds.Contains("STARRED"),
                HasAttachments: HasAttachments(payload),
                LabelIds: labelIds
            );
        }

        // Parcourt récursivement l'arbre MIME (multipart/*) pour trouver la partie text/plain,
        // avec repli sur text/html "dépouillé" si aucun text/plain n'existe.
        private static string? ExtractPlainTextBody(JsonElement payload)
        {
            var plainText = FindPartBody(payload, "text/plain");
            if (!string.IsNullOrWhiteSpace(plainText)) return plainText;

            var htmlText = FindPartBody(payload, "text/html");
            if (!string.IsNullOrWhiteSpace(htmlText)) return StripHtml(htmlText);

            return null;
        }

        private static string? FindPartBody(JsonElement part, string mimeType)
        {
            var currentMimeType = part.TryGetProperty("mimeType", out var mt) ? mt.GetString() : null;

            if (currentMimeType == mimeType
                && part.TryGetProperty("body", out var body)
                && body.TryGetProperty("data", out var data))
            {
                var encoded = data.GetString();
                return string.IsNullOrEmpty(encoded) ? null : DecodeBase64Url(encoded);
            }

            if (part.TryGetProperty("parts", out var parts))
            {
                foreach (var child in parts.EnumerateArray())
                {
                    var found = FindPartBody(child, mimeType);
                    if (found != null) return found;
                }
            }

            return null;
        }

        private static bool HasAttachments(JsonElement part)
        {
            if (part.TryGetProperty("filename", out var fn) && !string.IsNullOrEmpty(fn.GetString()))
                return true;

            if (part.TryGetProperty("parts", out var parts))
                return parts.EnumerateArray().Any(HasAttachments);

            return false;
        }

        private static string DecodeBase64Url(string base64Url)
        {
            var s = base64Url.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }

        private static string EncodeBase64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static string StripHtml(string html)
        {
            var withoutScripts = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var withBreaks = Regex.Replace(withoutScripts, "<(br|/p|/div|/tr|/li)[^>]*>", "\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var withoutTags = Regex.Replace(withBreaks, "<[^>]+>", " ");
            return System.Net.WebUtility.HtmlDecode(withoutTags)
                .Replace("\r\n", "\n")
                .Trim();
        }

        public async Task<string> CreateDraftAsync(
            GmailConnection connection, string to, string subject, string body,
            string? threadId, string? inReplyToRfc822MessageId)
        {
            var rawMime = BuildRfc822Message(connection.GmailAddress, to, subject, body, inReplyToRfc822MessageId);
            var raw = EncodeBase64Url(Encoding.UTF8.GetBytes(rawMime));

            object payload = string.IsNullOrEmpty(threadId)
                ? new { message = new { raw } }
                : new { message = new { raw, threadId } };

            using var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiBase}/drafts", connection, payload);
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await ReadBodyAsync(response, "drafts.create");

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        public async Task<string> SendDraftAsync(GmailConnection connection, string draftId)
        {
            using var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiBase}/drafts/send", connection, new { id = draftId });
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await ReadBodyAsync(response, "drafts.send");

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        private static string BuildRfc822Message(string from, string to, string subject, string body, string? inReplyTo)
        {
            // Header d'objet encodé en Base64 UTF-8 : les accents du sujet passent sans corruption.
            var sb = new StringBuilder();
            sb.Append("From: ").Append(from).Append("\r\n");
            sb.Append("To: ").Append(to).Append("\r\n");
            sb.Append("Subject: ").Append(EncodeHeaderUtf8(subject)).Append("\r\n");
            if (!string.IsNullOrEmpty(inReplyTo))
            {
                sb.Append("In-Reply-To: <").Append(inReplyTo).Append(">\r\n");
                sb.Append("References: <").Append(inReplyTo).Append(">\r\n");
            }
            sb.Append("Content-Type: text/plain; charset=\"UTF-8\"\r\n");
            sb.Append("Content-Transfer-Encoding: quoted-printable\r\n");
            sb.Append("MIME-Version: 1.0\r\n\r\n");
            // quoted-printable : lignes <= 76 caractères exigées par MIME, accents préservés.
            sb.Append(ToQuotedPrintable(body));
            return sb.ToString();
        }

        private static string EncodeHeaderUtf8(string value) =>
            "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)) + "?=";

        private static string ToQuotedPrintable(string text)
        {
            var sb = new StringBuilder();
            var lineLength = 0;
            var normalized = text.Replace("\r\n", "\n").Replace("\n", "\r\n");

            foreach (var ch in normalized)
            {
                if (ch == '\r') continue; // le CRLF est géré par la longueur de ligne

                // Chaque caractère peut produire plusieurs séquences =XX (UTF-8 sur 1 à 4 octets).
                var sequences = EncodeChar(ch);
                var rawLength = ch == ' ' ? 1 : sequences.Length;

                // Soft break : on coupe avant 75 caractères pour rester sous la limite de 76.
                if (lineLength + rawLength > 73)
                {
                    sb.Append("=\r\n");
                    lineLength = 0;
                }

                sb.Append(sequences);
                lineLength += rawLength;
            }
            return sb.ToString();
        }

        private static string EncodeChar(char ch)
        {
            if (ch == ' ' || (ch >= '!' && ch <= '~' && ch != '='))
                return ch.ToString();

            if (ch == '=')
                return "=3D";

            // > 126 : accents et caractères hors ASCII -> séquence =XX par octet UTF-8.
            var bytes = Encoding.UTF8.GetBytes(ch.ToString());
            var sb = new StringBuilder(bytes.Length * 3);
            foreach (var b in bytes)
                sb.Append('=').Append(b.ToString("X2", CultureInfo.InvariantCulture));

            return sb.ToString();
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
