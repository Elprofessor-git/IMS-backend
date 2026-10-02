using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend_Gestion_Magasin_API.Models.Gmail;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    public record GmailApiAttachment(
        string GmailAttachmentId,
        string? FileName,
        string? MimeType,
        long SizeBytes,
        bool IsInline,
        string? ContentId);

    public record GmailApiMessage(
        string Id,
        string ThreadId,
        string? From,
        string? To,
        string? Cc,
        string? Subject,
        string? Rfc822MessageId,
        string? BodyText,
        string? BodyHtml,
        string? Snippet,
        DateTime ReceivedAt,
        bool IsRead,
        bool IsStarred,
        bool HasAttachments,
        List<string> LabelIds,
        List<GmailApiAttachment> Attachments);

    public interface IGmailApiService
    {
        Task<List<string>> ListMessageIdsAsync(GmailConnection connection, string? query, int maxResults = 30, string? pageToken = null);
        Task<(List<string> Ids, string? NextPageToken)> ListMessageIdsPageAsync(GmailConnection connection, string? query, int maxResults = 30, string? pageToken = null);
        Task<GmailApiMessage> GetMessageAsync(GmailConnection connection, string gmailMessageId);

        /// <summary>
        /// Crée un brouillon, éventuellement porteur de pièces jointes (A4). Sans pièce
        /// jointe, le message est construit en text/plain simple : c'est ce qu'attendent
        /// les lecteurs qui n'affichent pas le HTML.
        /// </summary>
        Task<string> CreateDraftAsync(GmailConnection connection, string to, string subject, string body, string? threadId, string? inReplyToRfc822MessageId, IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null);

        /// <summary>
        /// Réécrit le contenu d'un brouillon Gmail existant (users.drafts.update).
        /// <para>
        /// Indispensable avant tout envoi : un brouillon figé depuis une version
        /// antérieure du texte CONTINUERAIT d'envoyer cette version périmée. Un simple
        /// « CreateDraft + Send » produirait un doublon et laisserait le vieux brouillon.
        /// </para>
        /// </summary>
        Task UpdateDraftAsync(GmailConnection connection, string draftId, string to, string subject, string body, string? threadId, string? inReplyToRfc822MessageId, IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null);

        Task<string> SendDraftAsync(GmailConnection connection, string draftId);
        Task DeleteDraftAsync(GmailConnection connection, string draftId);

        /// <summary>
        /// Ajoute et retire des étiquettes sur un message (users.messages.modify).
        /// Sert aux actions lu/non-lu, étoile et archive : Gmail reste la source de vérité,
        /// l'IMS ne fait que refléter l'état.
        /// </summary>
        Task ModifyMessageLabelsAsync(
            GmailConnection connection,
            string gmailMessageId,
            IReadOnlyCollection<string> addLabelIds,
            IReadOnlyCollection<string> removeLabelIds);

        /// <summary>Met un message à la corbeille (users.messages.trash).</summary>
        Task TrashMessageAsync(GmailConnection connection, string gmailMessageId);

        /// <summary>
        /// Variante au niveau du fil (users.threads.modify). Un seul appel pour toute la
        /// conversation : marquer 12 messages lus ne doit pas coûter 12 requêtes Gmail, et
        /// Gmail applique l'étiquette au fil entier, ce qui correspond à l'attente
        /// « marquer cette conversation comme lue ».
        /// </summary>
        Task ModifyThreadLabelsAsync(
            GmailConnection connection,
            string gmailThreadId,
            IReadOnlyCollection<string> addLabelIds,
            IReadOnlyCollection<string> removeLabelIds);

        /// <summary>Met un fil entier à la corbeille (users.threads.trash).</summary>
        Task TrashThreadAsync(GmailConnection connection, string gmailThreadId);
        Task<(string FileName, string MimeType, byte[] Content)> GetAttachmentAsync(GmailConnection connection, string gmailMessageId, string attachmentId);
        Task<(string MessageId, string ThreadId)> SendMessageAsync(
            GmailConnection connection, string to, string? cc, string? bcc, string? subject,
            string? bodyText, string? bodyHtml, string? threadId, string? inReplyTo,
            IReadOnlyList<(string FileName, string MimeType, byte[] Content)> attachments);
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

            var html = ExtractHtmlBody(payload);

            // Le corps est lu AVANT les pièces : la qualification « inline » dépend des
            // Content-ID que le corps référence réellement, pas de leur seule présence.
            var parts = new List<GmailApiAttachment>();
            var resolvableCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAttachments(
                payload, parts, CollectReferencedContentIds(html), resolvableCids);

            // Les cid: pointant vers une partie absente sont retirés : l'image n'existe pas,
            // mieux vaut ne rien afficher qu'afficher une icône cassée.
            html = DropUnresolvedCidReferences(html, resolvableCids);

            return new GmailApiMessage(
                Id: gmailId,
                ThreadId: threadId,
                From: GetHeader("From"),
                To: GetHeader("To"),
                Cc: GetHeader("Cc"),
                Subject: GetHeader("Subject"),
                Rfc822MessageId: string.IsNullOrEmpty(rfc822) ? null : rfc822,
                BodyText: ExtractPlainTextBody(payload),
                BodyHtml: html,
                Snippet: root.TryGetProperty("snippet", out var sn) ? sn.GetString() : null,
                ReceivedAt: receivedAt,
                IsRead: !labelIds.Contains("UNREAD"),
                IsStarred: labelIds.Contains("STARRED"),
                HasAttachments: parts.Any(a => !a.IsInline),
                LabelIds: labelIds,
                Attachments: parts
            );
        }

        /// <summary>Partie text/html du corps, ou null si l'email n'en a pas.</summary>
        private static string? ExtractHtmlBody(JsonElement payload) =>
            FindPartBody(payload, "text/html");

        /// <summary>
        /// Parcourt l'arbre MIME et relève les pièces jointes ET les images intégrées au corps.
        /// <para>
        /// Une partie est « inline » quand elle porte un en-tête <c>Content-ID</c> : c'est le
        /// mécanisme utilisé par Gmail et Outlook pour les logos et signatures. Les deux
        /// catégories alimentent la même table, avec <c>IsInline</c> pour les distinguer — la
        /// liste de pièces jointes de l'interface ne montre que les premières.
        /// </para>
        /// </summary>
        /// <param name="referencedCids">
        /// Content-ID référencés par un cid: du corps HTML, ou <c>null</c> si le corps n'est
        /// pas disponible (lecture « metadata ») : la double condition ne s'applique alors pas.
        /// </param>
        /// <param name="resolvableCids">Reçoit les Content-ID effectivement résolus, ou null.</param>
        private static void CollectAttachments(
            JsonElement part, List<GmailApiAttachment> results,
            IReadOnlySet<string>? referencedCids, ISet<string>? resolvableCids)
        {
            var mimeType = part.TryGetProperty("mimeType", out var mt) ? mt.GetString() : null;
            var fileName = part.TryGetProperty("filename", out var fn) ? fn.GetString() : null;
            var contentId = ReadHeader(part, "Content-ID")?.Trim().Trim('<', '>');
            var size = ReadPartSize(part);

            if (part.TryGetProperty("body", out var body)
                && body.TryGetProperty("attachmentId", out var attId)
                && attId.GetString() is { Length: > 0 } attachmentId)
            {
                // Content-Disposition est le critère MIME de référence. La présence d'un
                // Content-ID ne prouve RIEN : beaucoup de clients en mettent un sur des
                // pièces jointes ordinaires, qui se retrouvaient alors classées « inline »
                // et donc invisibles dans la liste — et le Content-ID était alors
                // réinjecté dans le corps par la réécriture, pour rien.
                var disposition = ReadHeader(part, "Content-Disposition")?.Trim();
                var isAttachment = IsExplicitAttachment(disposition);

                // « inline » n'a de sens que si le corps RÉFÉRENCE réellement cet
                // identifiant par cid:. Un Content-ID orphelin ne rend aucune image.
                var isInline = !isAttachment
                               && !string.IsNullOrEmpty(contentId)
                               && mimeType != null
                               && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                               && (referencedCids == null || referencedCids.Contains(contentId!));
                if (isInline) resolvableCids?.Add(contentId!);

                results.Add(new GmailApiAttachment(
                    GmailAttachmentId: attachmentId,
                    FileName: string.IsNullOrWhiteSpace(fileName) ? null : fileName,
                    MimeType: string.IsNullOrEmpty(mimeType) ? "application/octet-stream" : mimeType,
                    SizeBytes: size,
                    IsInline: isInline,
                    ContentId: isInline ? contentId : null));
            }

            if (part.TryGetProperty("parts", out var parts))
            {
                foreach (var child in parts.EnumerateArray())
                    CollectAttachments(child, results, referencedCids, resolvableCids);

            }
        }

        /// <summary>
        /// Taille en octets annoncée par Gmail pour une partie MIME, ou 0 si absente.
        /// <para>
        /// Le champ <c>body.size</c> est un NOMBRE en JSON, mais Google l'a déjà renvoyé
        /// entre guillemets sur certaines requêtes. Un <c>GetString()</c> direct lève alors
        /// une InvalidOperationException sur toute la lecture du message — c'est-à-dire que
        /// la simple présence d'une pièce jointe faisait échouer la synchronisation. On
        /// accepte donc les deux formes plutôt que de parier sur l'humeur de l'API.
        /// </para>
        /// </summary>
        private static long ReadPartSize(JsonElement part)
        {
            if (!part.TryGetProperty("body", out var body)
                || !body.TryGetProperty("size", out var size)) return 0;

            return size.ValueKind switch
            {
                JsonValueKind.Number when size.TryGetInt64(out var n) => n,
                JsonValueKind.String when long.TryParse(size.GetString(), out var s) => s,
                _ => 0
            };
        }

        /// <summary>Content-Disposition explicite en « attachment ». Un en-tête absent, vide ou
        /// valant « inline » ne l'emporte pas.
        /// </summary>
        private static bool IsExplicitAttachment(string? disposition)
        {
            if (string.IsNullOrWhiteSpace(disposition)) return false;

            var value = disposition.ToLowerInvariant();
            var semi = value.IndexOf(';');
            if (semi >= 0) value = value[..semi];

            return value.Trim() == "attachment";
        }

        /// <summary>
        /// Ensemble des Content-ID réellement référencés par un <c>src="cid:…"</c> dans le
        /// corps HTML. Les identifiants sont comparés sans les chevrons, comme le veut la RFC.
        /// </summary>
        internal static HashSet<string> CollectReferencedContentIds(string? html)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(html)) return result;

            foreach (Match m in CidReference.Matches(html))
            {
                var cid = m.Groups["cid"].Value.Trim();
                if (cid.Length > 0) result.Add(cid);
            }

            return result;
        }

        private static readonly Regex CidReference = new(
            """cid:(?<cid>[^"'>\s)]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Retire d'un corps HTML les images dont le cid: ne correspond à aucune partie
        /// réellement présente (pièce supprimée, expurgée, ou jamais reçue).
        /// <para>
        /// Indispensable avec la qualification stricte : une image inline est classée
        /// inline SI ET SEULEMENT SI son cid: est référencé ET qu'une partie correspondante
        /// existe. Sans ce nettoyage, une référence orpheline produirait une icône
        /// cassée dans le message au lieu d'une image tout simplement absente.
        /// </para>
        /// </summary>
        public static string DropUnresolvedCidReferences(string? html, IReadOnlySet<string> resolvableCids)
        {
            if (string.IsNullOrWhiteSpace(html)) return html ?? "";

            return CidReference.Replace(html, m =>
            {
                var cid = m.Groups["cid"].Value.Trim();
                return resolvableCids.Contains(cid) ? m.Value : "";
            });
        }

        /// <summary>Lit un en-tête MIME dans le tableau <c>headers</c> d'une partie.</summary>
        private static string? ReadHeader(JsonElement part, string name)
        {
            if (!part.TryGetProperty("headers", out var headers) || headers.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var h in headers.EnumerateArray())
            {
                if (h.TryGetProperty("name", out var n)
                    && string.Equals(n.GetString(), name, StringComparison.OrdinalIgnoreCase)
                    && h.TryGetProperty("value", out var v))
                    return v.GetString();
            }

            return null;
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

        /// <summary>
        /// Télécharge le contenu binaire d'une pièce jointe via Gmail. Relu à la demande
        /// plutôt que stocké : la source de vérité reste Gmail, et l'IMS ne conserve pas
        /// de mégaoctants d'archives.
        /// </summary>
        public async Task<(string FileName, string MimeType, byte[] Content)> GetAttachmentAsync(
            GmailConnection connection, string gmailMessageId, string attachmentId)
        {
            using var request = await CreateRequestAsync(
                HttpMethod.Get, $"{ApiBase}/messages/{Uri.EscapeDataString(gmailMessageId)}/attachments/{Uri.EscapeDataString(attachmentId)}", connection);
            using var response = await _httpClient.SendAsync(request);
            var body = await ReadBodyAsync(response, $"attachments.get({attachmentId})");

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data").GetString();
            if (string.IsNullOrEmpty(data)) return ("piece-jointe", "application/octet-stream", Array.Empty<byte>());

            var bytes = Convert.FromBase64String(data.Replace('-', '+').Replace('_', '/').PadRight((data.Length + 3) / 4 * 4, '='));
            return ("piece-jointe", "application/octet-stream", bytes);
        }

        /// <summary>
        /// Envoie un message composé par l'utilisateur (texte + HTML + pièces jointes).
        /// Construit un <c>multipart/mixed</c> : le texte brut est TOUJOURS présent, parce
        /// qu'un email dont le HTML s'affiche mal doit rester lisible. Le HTML est
        /// assaini avant envoi — il originates de la zone de saisie, on ne fait pas
        /// confiance au client.
        /// </summary>
        public async Task<(string MessageId, string ThreadId)> SendMessageAsync(
            GmailConnection connection, string to, string? cc, string? bcc, string? subject,
            string? bodyText, string? bodyHtml, string? threadId, string? inReplyTo,
            IReadOnlyList<(string FileName, string MimeType, byte[] Content)> attachments)
        {
            var rawMime = BuildMultipartMessage(
                connection.GmailAddress, to, cc, bcc, subject, bodyText, bodyHtml, inReplyTo, attachments);
            var raw = EncodeBase64Url(Encoding.UTF8.GetBytes(rawMime));

            object payload = string.IsNullOrEmpty(threadId)
                ? new { raw }
                : new { raw, threadId };

            using var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiBase}/messages/send", connection, payload);
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await ReadBodyAsync(response, "messages.send");

            using var doc = JsonDocument.Parse(responseBody);
            var messageId = doc.RootElement.GetProperty("id").GetString() ?? "";
            var sentThreadId = doc.RootElement.TryGetProperty("threadId", out var t) && !string.IsNullOrEmpty(t.GetString())
                ? t.GetString()!
                : threadId ?? messageId;

            return (messageId, sentThreadId);
        }

        private static string BuildMultipartMessage(
            string from, string to, string? cc, string? bcc, string subject,
            string? bodyText, string? bodyHtml, string? inReplyTo,
            IReadOnlyList<(string FileName, string MimeType, byte[] Content)> attachments)
        {
            // MIME exige des fins de ligne CRLF ; un saut de ligne LF seul produit un
            // message que certains clients refusent d'afficher.
            const string crlf = "\r\n";
            var boundary = "ims-" + Guid.NewGuid().ToString("N");
            var altBoundary = "ims-alt-" + Guid.NewGuid().ToString("N");

            var sb = new StringBuilder();
            sb.Append("From: ").Append(from).Append(crlf);
            sb.Append("To: ").Append(to).Append(crlf);
            if (!string.IsNullOrWhiteSpace(cc)) sb.Append("Cc: ").Append(cc).Append(crlf);
            if (!string.IsNullOrWhiteSpace(bcc)) sb.Append("Bcc: ").Append(bcc).Append(crlf);
            sb.Append("Subject: ").Append(EncodeHeaderUtf8(subject)).Append(crlf);
            sb.Append("Message-ID: <").Append(Guid.NewGuid().ToString("N")).Append("@ims.local>").Append(crlf);
            if (!string.IsNullOrEmpty(inReplyTo))
            {
                sb.Append("In-Reply-To: <").Append(inReplyTo).Append(">").Append(crlf);
                sb.Append("References: <").Append(inReplyTo).Append(">").Append(crlf);
            }
            sb.Append("MIME-Version: 1.0").Append(crlf);

            var hasAttachments = attachments.Count > 0;

            if (!hasAttachments)
            {
                // Sans pièce jointe, un corps unique suffit : on garde le format simple
                // (text/plain) ou enrichi (multipart/alternative).
                var sanitizedHtml = HtmlSanitizer.Sanitize(bodyHtml);
                if (!string.IsNullOrWhiteSpace(sanitizedHtml) && !string.IsNullOrWhiteSpace(bodyText))
                {
                    sb.Append("Content-Type: multipart/alternative; boundary=\"").Append(altBoundary).Append('"').Append(crlf);
                    sb.Append(crlf);
                    AppendTextPart(sb, "text/plain; charset=\"UTF-8\"", bodyText, altBoundary, crlf);
                    AppendHtmlPart(sb, sanitizedHtml, altBoundary, crlf);
                    sb.Append("--").Append(altBoundary).Append("--").Append(crlf);
                }
                else if (!string.IsNullOrWhiteSpace(sanitizedHtml))
                {
                    AppendHtmlPart(sb, sanitizedHtml, null, crlf);
                }
                else
                {
                    AppendTextPart(sb, "text/plain; charset=\"UTF-8\"", bodyText ?? "", null, crlf);
                }

                return sb.ToString();
            }

            sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append('"').Append(crlf);
            sb.Append(crlf);

            // Corps : multipart/alternative si les deux versions existent, sinon la seule disponible.
            var sanitized = HtmlSanitizer.Sanitize(bodyHtml);
            if (!string.IsNullOrWhiteSpace(sanitized) && !string.IsNullOrWhiteSpace(bodyText))
            {
                sb.Append("--").Append(boundary).Append(crlf);
                sb.Append("Content-Type: multipart/alternative; boundary=\"").Append(altBoundary).Append('"').Append(crlf);
                sb.Append(crlf);
                AppendTextPart(sb, "text/plain; charset=\"UTF-8\"", bodyText, altBoundary, crlf);
                AppendHtmlPart(sb, sanitized, altBoundary, crlf);
                sb.Append("--").Append(altBoundary).Append("--").Append(crlf);
            }
            else if (!string.IsNullOrWhiteSpace(sanitized))
            {
                sb.Append("--").Append(boundary).Append(crlf);
                AppendHtmlPart(sb, sanitized, null, crlf);
            }
            else
            {
                sb.Append("--").Append(boundary).Append(crlf);
                AppendTextPart(sb, "text/plain; charset=\"UTF-8\"", bodyText ?? "", null, crlf);
            }

            foreach (var (fileName, mimeType, content) in attachments)
            {
                sb.Append("--").Append(boundary).Append(crlf);
                sb.Append("Content-Type: ").Append(mimeType).Append("; name=\"").Append(EscapeMimeName(fileName)).Append('"').Append(crlf);
                sb.Append("Content-Transfer-Encoding: base64").Append(crlf);

                // Un nom de fichier contenant un guillemet ou un saut de ligne casserait
                // l'en-tête MIME : c'est la porte d'entrée d'un injection d'en-tête.
                sb.Append("Content-Disposition: attachment; filename=\"").Append(EscapeMimeName(fileName)).Append('"').Append(crlf);
                sb.Append(crlf);
                sb.Append(ToBase64BodyLines(content, crlf));
            }

            sb.Append("--").Append(boundary).Append("--").Append(crlf);
            return sb.ToString();
        }

        private static void AppendTextPart(StringBuilder sb, string contentType, string body, string? boundary, string crlf)
        {
            if (boundary != null) sb.Append("--").Append(boundary).Append(crlf);
            sb.Append("Content-Type: ").Append(contentType).Append(crlf);
            sb.Append("Content-Transfer-Encoding: quoted-printable").Append(crlf);
            sb.Append(crlf);
            sb.Append(ToQuotedPrintable(body));
            if (boundary != null) sb.Append(crlf);
        }

        private static void AppendHtmlPart(StringBuilder sb, string html, string? boundary, string crlf)
        {
            if (boundary != null) sb.Append("--").Append(boundary).Append(crlf);
            sb.Append("Content-Type: text/html; charset=\"UTF-8\"").Append(crlf);
            sb.Append("Content-Transfer-Encoding: quoted-printable").Append(crlf);
            sb.Append(crlf);
            sb.Append(ToQuotedPrintable(html));
            if (boundary != null) sb.Append(crlf);
        }

        /// <summary>Base64 découpé en lignes de 76 caractères, comme l'exige le transfert MIME.</summary>
        private static string ToBase64BodyLines(byte[] content, string crlf)
        {
            var base64 = Convert.ToBase64String(content);
            var sb = new StringBuilder(base64.Length + (base64.Length / 76 + 1) * 2);
            for (var i = 0; i < base64.Length; i += 76)
            {
                sb.Append(base64, i, Math.Min(76, base64.Length - i)).Append(crlf);
            }
            return sb.ToString();
        }

        /// <summary>Neutralise guillemets, CR et LF dans un nom de fichier destiné à un en-tête MIME.</summary>
        private static string EscapeMimeName(string name)
        {
            var flat = (name ?? "").Replace("\r", "").Replace("\n", "").Replace("\"", "'").Trim();
            return flat.Length == 0 ? "piece-jointe" : Truncate(flat, 200);
        }

        public async Task<string> CreateDraftAsync(
            GmailConnection connection, string to, string subject, string body,
            string? threadId, string? inReplyToRfc822MessageId,
            IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null)
        {
            var rawMime = attachments is { Count: > 0 }
                ? BuildMultipartMessage(
                    connection.GmailAddress, to, null, null, subject,
                    body, null, inReplyToRfc822MessageId, attachments)
                : BuildRfc822Message(connection.GmailAddress, to, subject, body, inReplyToRfc822MessageId);
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

        public async Task UpdateDraftAsync(
            GmailConnection connection, string draftId, string to, string subject, string body,
            string? threadId, string? inReplyToRfc822MessageId,
            IReadOnlyList<(string FileName, string MimeType, byte[] Content)>? attachments = null)
        {
            var rawMime = attachments is { Count: > 0 }
                ? BuildMultipartMessage(
                    connection.GmailAddress, to, null, null, subject,
                    body, null, inReplyToRfc822MessageId, attachments)
                : BuildRfc822Message(connection.GmailAddress, to, subject, body, inReplyToRfc822MessageId);
            var raw = EncodeBase64Url(Encoding.UTF8.GetBytes(rawMime));

            object payload = string.IsNullOrEmpty(threadId)
                ? new { message = new { raw } }
                : new { message = new { raw, threadId } };

            using var request = await CreateRequestAsync(
                HttpMethod.Put,
                $"{ApiBase}/drafts/{Uri.EscapeDataString(draftId)}",
                connection,
                payload);
            using var response = await _httpClient.SendAsync(request);
            await ReadBodyAsync(response, "drafts.update");
        }

        public async Task<string> SendDraftAsync(GmailConnection connection, string draftId)
        {
            using var request = await CreateRequestAsync(HttpMethod.Post, $"{ApiBase}/drafts/send", connection, new { id = draftId });
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await ReadBodyAsync(response, "drafts.send");

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        /// <summary>
        /// Supprime définitivement un brouillon Gmail. Un 404 n'est pas une erreur : le
        /// brouillon a pu être supprimé à la main depuis l'interface Gmail, et l'on ne
        /// veut pas bloquer le « Refuser » côté IMS pour autant.
        /// </summary>
        public async Task DeleteDraftAsync(GmailConnection connection, string draftId)
        {
            using var request = await CreateRequestAsync(HttpMethod.Delete, $"{ApiBase}/drafts/{Uri.EscapeDataString(draftId)}", connection);
            using var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Suppression du brouillon Gmail {DraftId} échouée : {Status} {Body}", draftId, response.StatusCode, TruncateForLog(body));
            throw new InvalidOperationException("Gmail n'a pas pu supprimer le brouillon.");
        }

        private static string TruncateForLog(string value) =>
            value.Length <= 500 ? value : value[..500] + "…";

        public async Task ModifyMessageLabelsAsync(
            GmailConnection connection,
            string gmailMessageId,
            IReadOnlyCollection<string> addLabelIds,
            IReadOnlyCollection<string> removeLabelIds)
        {
            // Rien à faire : l'appel serait un aller-retour Gmail sans aucun effet.
            if (addLabelIds.Count == 0 && removeLabelIds.Count == 0) return;

            var body = new
            {
                addLabelIds = addLabelIds.ToArray(),
                removeLabelIds = removeLabelIds.ToArray()
            };

            using var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"{ApiBase}/messages/{Uri.EscapeDataString(gmailMessageId)}/modify",
                connection,
                body);
            using var response = await _httpClient.SendAsync(request);
            await ReadBodyAsync(response, $"messages.modify({gmailMessageId})");
        }

        public async Task TrashMessageAsync(GmailConnection connection, string gmailMessageId)
        {
            using var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"{ApiBase}/messages/{Uri.EscapeDataString(gmailMessageId)}/trash",
                connection);
            using var response = await _httpClient.SendAsync(request);
            await ReadBodyAsync(response, $"messages.trash({gmailMessageId})");
        }

        public async Task ModifyThreadLabelsAsync(
            GmailConnection connection,
            string gmailThreadId,
            IReadOnlyCollection<string> addLabelIds,
            IReadOnlyCollection<string> removeLabelIds)
        {
            if (addLabelIds.Count == 0 && removeLabelIds.Count == 0) return;

            var body = new
            {
                addLabelIds = addLabelIds.ToArray(),
                removeLabelIds = removeLabelIds.ToArray()
            };

            using var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"{ApiBase}/threads/{Uri.EscapeDataString(gmailThreadId)}/modify",
                connection,
                body);
            using var response = await _httpClient.SendAsync(request);
            await ReadBodyAsync(response, $"threads.modify({gmailThreadId})");
        }

        public async Task TrashThreadAsync(GmailConnection connection, string gmailThreadId)
        {
            using var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"{ApiBase}/threads/{Uri.EscapeDataString(gmailThreadId)}/trash",
                connection);
            using var response = await _httpClient.SendAsync(request);
            await ReadBodyAsync(response, $"threads.trash({gmailThreadId})");
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
