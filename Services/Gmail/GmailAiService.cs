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

    public interface IGmailAiService
    {
        /// <summary>False si GROQ_API_KEY n'est pas configurée : permet un message clair côté UI.</summary>
        bool IsAvailable { get; }
        Task<TaskSuggestionResult> AnalyzeForTaskAsync(string emailFrom, string? subject, string? body);
        Task<string> GenerateReplyAsync(string emailFrom, string? subject, string? body, string? instruction);
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
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Réponse IA non-JSON pour l'analyse de tâche : {Raw}", raw);
                return new TaskSuggestionResult { IsTask = false, Confidence = 0 };
            }
        }

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

        private async Task<string> CallGroqJsonAsync(string systemPrompt, string userPrompt)
        {
            var content = await SendAsync(
                systemPrompt, userPrompt, temperature: 0.2,
                extraBody: new Dictionary<string, object?>
                {
                    ["response_format"] = new { type = "json_object" }
                },
                context: "analyse de tâche");

            try
            {
                using var doc = JsonDocument.Parse(content);
                return doc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString() ?? "{}";
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Réponse Groq malformée (analyse de tâche) : {Content}", Truncate(content, 400));
                return "{}";
            }
        }

        private async Task<string> CallGroqTextAsync(string systemPrompt, string userPrompt) =>
            (await SendAsync(systemPrompt, userPrompt, temperature: 0.4, extraBody: null, context: "génération de réponse")).Trim();

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

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? "";
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

        /// <summary>Convertit une date "yyyy-MM-dd" (contrat du modèle IA) en DateTime UTC, ou null si inexploitable.</summary>
        public static DateTime? ParseSuggestedDueDate(string? value) =>
            DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                ? parsed
                : null;
    }
}
