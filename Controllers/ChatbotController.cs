using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly AiChatService _aiChatService;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(AiChatService aiChatService, ILogger<ChatbotController> logger)
        {
            _aiChatService = aiChatService;
            _logger = logger;
        }

        /// <summary>
        /// Envoie un message au chatbot IA et retourne la réponse
        /// </summary>
        /// <param name="request">La requête contenant le message à envoyer</param>
        /// <returns>La réponse du chatbot</returns>
        [HttpPost("chat")]
        [Authorize] // Nécessite une authentification
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { error = "Le message ne peut pas être vide." });
                }

                // Récupérer l'ID de l'utilisateur connecté
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                request.UserId = userId;

                _logger.LogInformation($"Requête de chat reçue de l'utilisateur {userId}: {request.Message}");

                var response = await _aiChatService.SendMessageAsync(request);

                if (response.Success)
                {
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning($"Échec du traitement du message: {response.Error}");
                    return StatusCode(500, new { 
                        error = "Erreur lors du traitement de votre message.",
                        details = response.Error 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans ChatbotController.Chat");
                return StatusCode(500, new { 
                    error = "Une erreur inattendue s'est produite.",
                    details = ex.Message 
                });
            }
        }

        /// <summary>
        /// Envoie un message au chatbot IA sans authentification (pour les utilisateurs anonymes)
        /// </summary>
        /// <param name="request">La requête contenant le message à envoyer</param>
        /// <returns>La réponse du chatbot</returns>
        [HttpPost("chat/anonymous")]
        public async Task<IActionResult> ChatAnonymous([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { error = "Le message ne peut pas être vide." });
                }

                // Pour les utilisateurs anonymes, on peut utiliser un ID de session
                if (string.IsNullOrWhiteSpace(request.SessionId))
                {
                    request.SessionId = Guid.NewGuid().ToString();
                }

                _logger.LogInformation($"Requête de chat anonyme reçue avec session {request.SessionId}: {request.Message}");

                var response = await _aiChatService.SendMessageAsync(request);

                if (response.Success)
                {
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning($"Échec du traitement du message anonyme: {response.Error}");
                    return StatusCode(500, new { 
                        error = "Erreur lors du traitement de votre message.",
                        details = response.Error 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans ChatbotController.ChatAnonymous");
                return StatusCode(500, new { 
                    error = "Une erreur inattendue s'est produite.",
                    details = ex.Message 
                });
            }
        }

        /// <summary>
        /// Vérifie si le service de chatbot est disponible
        /// </summary>
        /// <returns>Le statut de disponibilité du service</returns>
        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                var isAvailable = await _aiChatService.IsApiAvailableAsync();
                
                return Ok(new { 
                    status = isAvailable ? "available" : "unavailable",
                    timestamp = DateTime.UtcNow,
                    service = "AI Chatbot"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification de l'état du service chatbot");
                return StatusCode(500, new { 
                    status = "error",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow,
                    service = "AI Chatbot"
                });
            }
        }

        /// <summary>
        /// Obtient des informations sur le chatbot
        /// </summary>
        /// <returns>Les informations du chatbot</returns>
        [HttpGet("info")]
        public IActionResult GetChatbotInfo()
        {
            return Ok(new
            {
                name = "Assistant IA Gestion Magasin",
                version = "1.0.0",
                description = "Assistant intelligent pour la gestion de magasin",
                capabilities = new[]
                {
                    "Répondre aux questions sur la gestion de stock",
                    "Aider avec les commandes et les fournisseurs",
                    "Fournir des informations sur les produits",
                    "Assistance générale pour l'application"
                },
                endpoints = new
                {
                    authenticated_chat = "/api/chatbot/chat",
                    anonymous_chat = "/api/chatbot/chat/anonymous",
                    health_check = "/api/chatbot/health",
                    info = "/api/chatbot/info"
                }
            });
        }
    }
}

