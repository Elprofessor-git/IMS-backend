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
        private readonly ChatbotAgentService _agentService;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(ChatbotAgentService agentService, ILogger<ChatbotController> logger)
        {
            _agentService = agentService;
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

                _logger.LogInformation("Chat user={UserId}: {Message}", request.UserId, request.Message);

                var response = await _agentService.SendMessageAsync(request);

                return response.Success
                    ? Ok(response)
                    : StatusCode(500, new { error = "Erreur lors du traitement.", details = response.Error });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans ChatbotController.Chat");
                return StatusCode(500, new { error = "Une erreur inattendue s'est produite.", details = ex.Message });
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                var isAvailable = await _agentService.IsAvailableAsync();
                return Ok(new
                {
                    status    = isAvailable ? "available" : "unavailable",
                    timestamp = DateTime.UtcNow,
                    service   = "Groq AI",
                    model     = _agentService.CurrentModel
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur vérification état Groq");
                return StatusCode(500, new { status = "error", error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        [HttpGet("info")]
        public IActionResult GetChatbotInfo()
        {
            return Ok(new
            {
                name        = "Assistant IA Gestion Magasin",
                version     = "2.0.0",
                engine      = $"Groq — {_agentService.CurrentModel}",
                description = "Assistant intelligent pour la gestion de magasin textile (lecture seule)",
                capabilities = new[]
                {
                    "Rechercher des articles et consulter le stock en temps réel",
                    "Détecter les articles sous seuil d'alerte ou critique",
                    "Suivre les commandes clients par statut et marque",
                    "Consulter les achats fournisseurs",
                    "Consulter les importations",
                    "Historique des mouvements de stock"
                },
                endpoints = new
                {
                    authenticated_chat = "/api/chatbot/chat",
                    health_check       = "/api/chatbot/health",
                    info               = "/api/chatbot/info"
                }
            });
        }
    }
}
