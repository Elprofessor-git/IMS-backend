using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend_Gestion_Magasin_API.Services.Gmail
{
    public class TaskSuggestionResult
    {
        [JsonPropertyName("isTask")] public bool IsTask { get; set; }
        [JsonPropertyName("confidence")] public decimal Confidence { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("priority")] public string? Priority { get; set; } // "Basse"|"Normale"|"Haute"
        [JsonPropertyName("dueDate")] public string? DueDate { get; set; }   // "yyyy-MM-dd" ou null
    }

    /// <summary>Langues de traduction autorisées. Liste fermée : rien d'autre n'est accepté.</summary>
    public static class TranslateLanguage
    {
        public const string French = "FR";
        public const string English = "EN";
        public const string Arabic = "AR";

        public static readonly string[] All = { French, English, Arabic };

        public static bool IsSupported(string? value) =>
            value != null && All.Contains(value.Trim().ToUpperInvariant(), StringComparer.Ordinal);

        /// <summary>Libellé affiché dans le sélecteur de l'interface.</summary>
        public static string DisplayName(string code) => code.ToUpperInvariant() switch
        {
            French => "Français",
            English => "English",
            Arabic => "العربية",
            _ => code
        };
    }

    /// <summary>Opérations d'édition proposées sur le brouillon en cours.</summary>
    public enum DraftEditAction
    {
        /// <summary>Reformulation libre à partir d'une instruction en langage naturel.</summary>
        Rewrite,

        /// <summary>Traduction vers une des trois langues autorisées.</summary>
        Translate
    }

    public interface IGmailAiService
    {
        /// <summary>False si GROQ_API_KEY n'est pas configurée : permet un message clair côté UI.</summary>
        bool IsAvailable { get; }
        Task<TaskSuggestionResult> AnalyzeForTaskAsync(string emailFrom, string? subject, string? body);
        Task<string> GenerateReplyAsync(string emailFrom, string? subject, string? body, string? instruction);

        /// <summary>
        /// Reformule ou traduit le texte SAISSI par l'utilisateur dans la zone de composition.
        /// Le texte fourni est la seule source : aucun email, aucun fil et aucune donnée IMS
        /// ne sont transmis au modèle.
        /// </summary>
        Task<string> EditDraftTextAsync(DraftEditAction action, string text, string? instruction, string? targetLanguage);
    }

    // Réutilise la même API Groq que l'assistant IA existant du backend (GROQ_API_KEY / GroqSettings:Model),
    // mais en appel direct (sans function-calling/tools : ici on veut un JSON de sortie, pas une conversation).
    public class GmailAiService : IGmailAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GmailAiService> _logger;

        private const string GroqChatEndpoint = "https://api.groq.com/openai/v1/chat/completions";

        public GmailAiService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GmailAiService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("groq");
            _configuration = configuration;
            _logger = logger;
        }

        public bool IsAvailable => !string.IsNullOrWhiteSpace(ApiKeyOrNull);

        private string? ApiKeyOrNull =>
            Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? _configuration["GroqSettings:ApiKey"];

        private string ApiKey => ApiKeyOrNull
            ?? throw new InvalidOperationException("GROQ_API_KEY manquante — l'assistance IA sur les courriels est désactivée.");

        private string Model => _configuration["GroqSettings:Model"] ?? "openai/gpt-oss-120b";

        public async Task<TaskSuggestionResult> AnalyzeForTaskAsync(string emailFrom, string? subject, string? body)
        {
            const string systemPrompt = """
                Tu analyses un email reçu dans un atelier de confection textile (ERP IMS).
                Détermine si cet email contient une DEMANDE D'ACTION nécessitant la création d'une tâche
                de suivi (ex: vérifier un stock, répondre à une commande, relancer un fournisseur, corriger un problème).
                Un email purement informatif (accusé de réception, newsletter, confirmation sans action requise) n'est PAS une tâche.

                Réponds STRICTEMENT en JSON, sans aucun texte avant/après, avec ce schéma exact :
                {"isTask": bool, "confidence": number entre 0 et 1, "title": string|null, "description": string|null,
                 "priority": "Basse"|"Normale"|"Haute"|null, "dueDate": "yyyy-MM-dd"|null}

                Si isTask=false, mets title/description/priority/dueDate à null.
                La date d'échéance ne doit être renseignée QUE si elle est explicitement mentionnée ou clairement déductible du texte.
                """;

            var userPrompt = $"De: {emailFrom}\nObjet: {subject}\n\n{Truncate(body, 4000)}";

            var raw = await CallGroqJsonAsync(systemPrompt, userPrompt);
            try
            {
                var result = JsonSerializer.Deserialize<TaskSuggestionResult>(StripCodeFences(raw));
                if (result == null)
                    return new TaskSuggestionResult { IsTask = false };

                // Garde-fou : une confiance hors [0,1] casserait la colonne numeric(5,4).
                result.Confidence = Math.Clamp(result.Confidence, 0m, 1m);

                // Garde-fou : les colonnes font varchar(255) (titre) et varchar(50) (priorité).
                // Un modèle bavard ferait échouer l'INSERT par une violation de longueur,
                // qui remonterait en 500 alors que la suggestion est parfaitement exploitable.
                result.Title = Clamp(result.Title, MaxTitleLength);
                result.Priority = Clamp(result.Priority, MaxPriorityLength);

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Réponse IA non-JSON pour l'analyse de tâche : {Raw}", raw);
                return new TaskSuggestionResult { IsTask = false, Confidence = 0 };
            }
        }

        /// <summary>Longueurs des colonnes <c>EmailAiAnalyses</c> (migration LOT 15).</summary>
        public const int MaxTitleLength = 255;
        public const int MaxPriorityLength = 50;

        public async Task<string> GenerateReplyAsync(string emailFrom, string? subject, string? body, string? instruction)
        {
            const string systemPrompt = """
                Tu rédiges, en français, un brouillon de réponse professionnelle et concise à un email reçu
                dans un atelier de confection textile. Ton poli, direct, pas de formules creuses excessives.
                Ne signe pas avec un nom précis (laisse "Cordialement," seul en fin de message — l'utilisateur signera).
                Réponds UNIQUEMENT avec le corps du message (pas d'objet, pas de JSON, pas de balises).
                """;

            var instructionLine = string.IsNullOrWhiteSpace(instruction)
                ? ""
                : $"\n\nConsigne de l'utilisateur pour cette réponse : {instruction}";

            var userPrompt = $"Email reçu de {emailFrom}, objet « {subject} » :\n\n{Truncate(body, 4000)}{instructionLine}";

            return (await CallGroqTextAsync(systemPrompt, userPrompt)).Trim();
        }

        public async Task<string> EditDraftTextAsync(
            DraftEditAction action, string text, string? instruction, string? targetLanguage)
        {
            // Garde-fou serveur : la liste fermée des langues est validée ici aussi, pas
            // seulement dans le contrôleur, pour que tout appelant futur soit contraint.
            if (action == DraftEditAction.Translate && !TranslateLanguage.IsSupported(targetLanguage))
                throw new ArgumentException("Langue de traduction non prise en charge.", nameof(targetLanguage));

            var source = (text ?? "").Trim();
            if (source.Length == 0)
                throw new ArgumentException("Le texte à reformuler est vide.", nameof(text));

            var (systemPrompt, userPrompt, context) = action switch
            {
                DraftEditAction.Translate => (
                    """
                    Tu es un traducteur professionnel. Tu traduis le texte fourni par l'utilisateur
                    dans la langue demandée, et tu ne rends QUE la traduction.
                    Conserve la mise en forme (retours à la ligne, listes), les montants, les dates,
                    les références produits et le ton d'origine. N'ajoute aucun commentaire,
                    aucune introduction et n.Utilise pas de markdown.
                    """,
                    $"Traduis ce texte en {TranslateLanguage.DisplayName(targetLanguage!)} :\n\n{source}",
                    "traduction de brouillon"),

                _ => (
                    """
                    Tu réécris le texte fourni par l'utilisateur en appliquant sa consigne.
                    Tu rends UNIQUEMENT le texte réécrit : pas de commentaire, pas d'explication,
                    pas de guillemets autour du résultat, pas de markdown.
                    Si la consigne est absente, tu ameliores la formulation et la clarté sans changer le sens.
                    Conserve les montants, dates, références produits et informations techniques.
                    """,
                    string.IsNullOrWhiteSpace(instruction)
                        ? $"Réécris ce texte :\n\n{source}"
                        : $"Consigne : {instruction.Trim()}\n\nTexte à réécrire :\n\n{source}",
                    "reformulation de brouillon")
            };

            var result = (await CallGroqTextAsync(systemPrompt, userPrompt, context: context)).Trim();

            if (result.Length == 0)
                throw new InvalidOperationException("L'assistance IA n'a renvoyé aucun texte.");

            return result;
        }

        private async Task<string> CallGroqJsonAsync(string systemPrompt, string userPrompt)
        {
            // SendAsync renvoie DÉJÀ le contenu du premier choix (choices[0].message.content),
            // pas l'enveloppe complète. On ne le déballe donc pas une seconde fois : le faire
            // levait un KeyNotFoundException sur la propriété « choices », absente du JSON
            // produit par le modèle — d'où un 500 brut sur le seul bouton « Analyser ».
            var content = (await SendAsync(
                systemPrompt, userPrompt, temperature: 0.2,
                extraBody: new Dictionary<string, object?>
                {
                    ["response_format"] = new { type = "json_object" }
                },
                context: "analyse de tâche")).Trim();

            // Contenu vide (raisonnement consommé sans réponse, quota épuisé…) : on rend un
            // objet vide exploitable plutôt que de faire échouer la désérialisation plus bas.
            return content.Length == 0 ? "{}" : content;
        }

        private Task<string> CallGroqTextAsync(string systemPrompt, string userPrompt, string context = "génération de réponse") =>
            SendAsync(systemPrompt, userPrompt, temperature: 0.4, extraBody: null, context: context);

        private async Task<string> SendAsync(string systemPrompt, string userPrompt, double temperature, IDictionary<string, object?>? extraBody, string context)
        {
            var body = new Dictionary<string, object?>
            {
                ["model"] = Model,
                ["messages"] = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                ["temperature"] = temperature
            };
            // response_format doit être fusionné à la racine du payload, pas imbriqué.
            if (extraBody != null)
            {
                foreach (var kv in extraBody) body[kv.Key] = kv.Value;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, GroqChatEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = JsonContent.Create(body);

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Appel Groq échoué ({Context}) : {Status} {Body}", context, response.StatusCode, Truncate(responseBody, 500));
                throw new InvalidOperationException("Le service IA n'a pas pu traiter cet email. Réessayez dans un instant.");
            }

            return ExtractFirstChoiceContent(responseBody, context);
        }

        /// <summary>
        /// Extrait <c>choices[0].message.content</c> de l'enveloppe Groq.
        /// Toute forme anormale (JSON invalide, « choices » absent ou vide, « content »
        /// manquant) est convertie en <see cref="InvalidOperationException"/> : c'est le seul
        /// type que les contrôleurs traduisent en erreur contrôlée. Un GetProperty/[] direct
        /// lèverait KeyNotFoundException ou IndexOutOfRangeException, non capturés, donc
        /// transformés en 500 brut avec la stack trace dans le corps de la réponse.
        /// </summary>
        private string ExtractFirstChoiceContent(string responseBody, string context)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (!doc.RootElement.TryGetProperty("choices", out var choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("Réponse vide du service IA.");
                }

                var message = choices[0];
                if (message.TryGetProperty("message", out var messageEl)
                    && messageEl.TryGetProperty("content", out var contentEl)
                    && contentEl.ValueKind == JsonValueKind.String)
                {
                    return contentEl.GetString() ?? "";
                }

                // content absent OU null : les modèles de raisonnement le laissent vide
                // quand le raisonnement a consommé tous les tokens. Traité comme vide.
                return "";
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Réponse Groq illisible ({Context}) : {Body}", context, Truncate(responseBody, 400));
                throw new InvalidOperationException("Le service IA a renvoyé une réponse illisible. Réessayez dans un instant.");
            }
        }

        // Certains modèles entourent le JSON de ```json ... ``` : on retire ces fences.
        private static string StripCodeFences(string raw)
        {
            var trimmed = raw.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;

            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine < 0) return trimmed;

            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence <= firstNewLine) return trimmed;

            return trimmed[(firstNewLine + 1)..lastFence].Trim();
        }

        private static string Truncate(string? s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

        /// <summary>
        /// Tronque à STRICTEMENT <paramref name="max"/> caractères, sans suffixe : une valeur
        /// à la longueur exacte de la colonne reste acceptée, et le résultat ne peut jamais
        /// dépasser la colonne (l'ajout d'une ellipse de <see cref="Truncate"/> le ferait).
        /// </summary>
        private static string? Clamp(string? value, int max) =>
            value == null ? null : value.Length <= max ? value : value[..max].TrimEnd();

        /// <summary>Convertit une date "yyyy-MM-dd" (contrat du modèle IA) en DateTime UTC, ou null si inexploitable.</summary>
        public static DateTime? ParseSuggestedDueDate(string? value) =>
            DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                ? parsed
                : null;
    }
}
