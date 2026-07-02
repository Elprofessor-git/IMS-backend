using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend_Gestion_Magasin_API.Services
{
    public class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ILogger<GroqService> _logger;

        private const string CompletionsUrl = "https://api.groq.com/openai/v1/chat/completions";

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public GroqService(HttpClient httpClient, IConfiguration config, ILogger<GroqService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")
                ?? config["GroqSettings:ApiKey"]
                ?? throw new InvalidOperationException(
                    "GROQ_API_KEY non configuré. Définir la variable d'environnement GROQ_API_KEY.");

            _model = Environment.GetEnvironmentVariable("GROQ_MODEL")
                ?? config["GroqSettings:Model"]
                ?? "llama-3.3-70b-versatile";
        }

        public string Model => _model;

        public async Task<JsonNode> CompleteAsync(IList<object> messages, object[] tools)
        {
            var payload = new
            {
                model       = _model,
                messages,
                tools,
                tool_choice = "auto",
                max_tokens  = 1024,
                temperature = 0.3
            };

            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            using var req = new HttpRequestMessage(HttpMethod.Post, CompletionsUrl);
            req.Headers.Add("Authorization", $"Bearer {_apiKey}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Groq API {Status}: {Body}", (int)resp.StatusCode, body);
                throw new HttpRequestException($"Groq API {(int)resp.StatusCode}: {body}");
            }

            return JsonNode.Parse(body)
                ?? throw new InvalidOperationException("Réponse Groq nulle ou malformée.");
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    "https://api.groq.com/openai/v1/models");
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");
                var resp = await _httpClient.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
