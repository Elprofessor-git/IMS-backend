using Backend_Gestion_Magasin_API.Models;
using System.Text;
using System.Text.Json;

namespace Backend_Gestion_Magasin_API.Services
{
    public class AiChatService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiChatService> _logger;

        public AiChatService(HttpClient httpClient, IConfiguration configuration, ILogger<AiChatService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
        {
            try
            {
                // Récupérer l'URL de l'API FastAPI depuis la configuration
                var fastApiUrl = _configuration["FastApiSettings:BaseUrl"] ?? "http://localhost:8000";
                var endpoint = $"{fastApiUrl}/chat";

                // Préparer les données à envoyer
                var requestData = new
                {
                    message = request.Message,
                    user_id = request.UserId,
                    session_id = request.SessionId
                };

                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Envoi de la requête vers l'API FastAPI: {endpoint}");

                // Envoyer la requête POST
                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Réponse reçue de l'API FastAPI: {responseContent}");

                    // Désérialiser la réponse
                    var apiResponse = JsonSerializer.Deserialize<dynamic>(responseContent);
                    
                    return new ChatResponse
                    {
                        Response = apiResponse?.GetProperty("response").GetString() ?? "Réponse vide",
                        SessionId = apiResponse?.GetProperty("session_id").GetString() ?? request.SessionId,
                        Success = true,
                        Timestamp = DateTime.UtcNow
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Erreur de l'API FastAPI: {response.StatusCode} - {errorContent}");

                    return new ChatResponse
                    {
                        Response = "Désolé, une erreur s'est produite lors du traitement de votre message.",
                        Success = false,
                        Error = $"API Error: {response.StatusCode} - {errorContent}",
                        SessionId = request.SessionId
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erreur de connexion à l'API FastAPI");
                return new ChatResponse
                {
                    Response = "Désolé, le service de chat n'est pas disponible pour le moment.",
                    Success = false,
                    Error = $"Connection Error: {ex.Message}",
                    SessionId = request.SessionId
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout lors de la requête à l'API FastAPI");
                return new ChatResponse
                {
                    Response = "Désolé, la requête a pris trop de temps à traiter.",
                    Success = false,
                    Error = $"Timeout Error: {ex.Message}",
                    SessionId = request.SessionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans AiChatService");
                return new ChatResponse
                {
                    Response = "Désolé, une erreur inattendue s'est produite.",
                    Success = false,
                    Error = $"Unexpected Error: {ex.Message}",
                    SessionId = request.SessionId
                };
            }
        }

        public async Task<bool> IsApiAvailableAsync()
        {
            try
            {
                var fastApiUrl = _configuration["FastApiSettings:BaseUrl"] ?? "http://localhost:8000";
                var healthEndpoint = $"{fastApiUrl}/health";

                var response = await _httpClient.GetAsync(healthEndpoint);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

