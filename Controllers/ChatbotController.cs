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
        private readonly GroqChatService _chatService;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(GroqChatService chatService, ILogger<ChatbotController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpPost("chat")]
        [Authorize]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return BadRequest(new { error = "Le message ne peut pas être vide." });

                request.UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                _logger.LogInformation("Requête chat de l'utilisateur {UserId}: {Message}", request.UserId, request.Message);

                var response = await _chatService.SendMessageAsync(request);

                return response.Success
                    ? Ok(response)
                    : StatusCode(500, new { error = "Erreur lors du traitement de votre message.", details = response.Error });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans ChatbotController.Chat");
                return StatusCode(500, new { error = "Une erreur inattendue s'est produite.", details = ex.Message });
            }
        }

        [HttpPost("chat/anonymous")]
        public async Task<IActionResult> ChatAnonymous([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return BadRequest(new { error = "Le message ne peut pas être vide." });

                if (string.IsNullOrWhiteSpace(request.SessionId))
                    request.SessionId = Guid.NewGuid().ToString();

                _logger.LogInformation("Requête chat anonyme session {SessionId}: {Message}", request.SessionId, request.Message);

                var response = await _chatService.SendMessageAsync(request);

                return response.Success
                    ? Ok(response)
                    : StatusCode(500, new { error = "Erreur lors du traitement de votre message.", details = response.Error });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans ChatbotController.ChatAnonymous");
                return StatusCode(500, new { error = "Une erreur inattendue s'est produite.", details = ex.Message });
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                var isAvailable = await _chatService.IsApiAvailableAsync();
                return Ok(new
                {
                    status = isAvailable ? "available" : "unavailable",
                    timestamp = DateTime.UtcNow,
                    service = "Groq AI"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification de l'état du service Groq");
                return StatusCode(500, new { status = "error", error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        [HttpGet("info")]
        public IActionResult GetChatbotInfo()
        {
            return Ok(new
            {
                name = "Assistant IA Gestion Magasin",
                version = "2.0.0",
                engine = "Groq — llama-3.3-70b-versatile",
                description = "Assistant intelligent pour la gestion de magasin textile",
                capabilities = new[]
                {
                    "Consulter les articles et le stock en temps réel",
                    "Détecter les alertes de stock",
                    "Suivre les commandes clients",
                    "Consulter les importations et achats"
                },
                endpoints = new
                {
                    authenticated_chat = "/api/chatbot/chat",
                    anonymous_chat     = "/api/chatbot/chat/anonymous",
                    health_check       = "/api/chatbot/health",
                    info               = "/api/chatbot/info"
                }
            });
        }
    }
}
