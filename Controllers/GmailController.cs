using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Gmail;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Models.Gmail;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Services.Gmail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/gmail")]
    [ApiController]
    [Authorize]
    public class GmailController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IGmailOAuthService _oauth;
        private readonly IGmailApiService _gmailApi;
        private readonly IGmailSyncService _sync;
        private readonly IGmailAiService _ai;
        private readonly ITokenEncryptionService _tokens;
        private readonly ILogger<GmailController> _logger;
        // LOT 16 — intégration Email → Tâches : attribution de propriété de la tâche créée.
        // Ce sont les SEULES dépendances ajoutées à ce contrôleur ; l'isolation Gmail
        // (CurrentUserId, GetOwnedMessageAsync, state, AES-GCM) est inchangée.
        private readonly ITacheOwnershipService _tacheOwnership;
        private readonly IPermissionService _permissions;
        // LOT 17 — la cloche : une tâche née d'un email prévient son responsable. Même table
        // et mêmes endpoints que les notifications de planning.
        private readonly INotificationService _notifications;

        public GmailController(
            ApplicationDbContext context,
            IGmailOAuthService oauth,
            IGmailApiService gmailApi,
            IGmailSyncService sync,
            IGmailAiService ai,
            ITokenEncryptionService tokens,
            ITacheOwnershipService tacheOwnership,
            IPermissionService permissions,
            INotificationService notifications,
            ILogger<GmailController> logger)
        {
            _tokens = tokens;
            _context = context;
            _oauth = oauth;
            _gmailApi = gmailApi;
            _sync = sync;
            _ai = ai;
            _tacheOwnership = tacheOwnership;
            _permissions = permissions;
            _notifications = notifications;
            _logger = logger;
        }

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Session invalide.");

        private string CurrentUserName => User.Identity?.Name ?? CurrentUserId;

        // ── Connexion / déconnexion ───────────────────────────────────────────

        // GET: api/gmail/status — état de la connexion + compteurs pour le bandeau du module
        [HttpGet("status")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<ActionResult<GmailStatusDto>> GetStatus()
        {
            var userId = CurrentUserId;

            var connection = await _context.GmailConnections
                .Where(c => c.UserId == userId && c.IsActive)
                .OrderByDescending(c => c.ConnectedAt)
                .FirstOrDefaultAsync();

            if (connection == null)
            {
                return Ok(new GmailStatusDto
                {
                    Connected = false,
                    Configure = _oauth.IsConfigured,
                    AiAvailable = _ai.IsAvailable,
                    TokenStorageReady = _tokens.IsAvailable
                });
            }

            var messageCount = await _context.GmailMessages
                .CountAsync(m => m.GmailConnectionId == connection.Id);
            var unreadCount = await _context.GmailMessages
                .CountAsync(m => m.GmailConnectionId == connection.Id && !m.IsRead);

            return Ok(new GmailStatusDto
            {
                Connected = true,
                GmailAddress = connection.GmailAddress,
                ConnectedAt = connection.ConnectedAt,
                LastSyncAt = connection.LastSyncAt,
                Configure = _oauth.IsConfigured,
                AiAvailable = _ai.IsAvailable,
                TokenStorageReady = _tokens.IsAvailable,
                MessageCount = messageCount,
                UnreadCount = unreadCount
            });
        }

        // GET: api/gmail/connect — URL d'autorisation Google (le state est signé et expire en 10 min)
        [HttpGet("connect")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public ActionResult<GmailConnectStartDto> Connect()
        {
            if (!_oauth.IsConfigured)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "La connexion Gmail n'est pas configurée sur ce serveur (Google:ClientId / Google:ClientSecret / Google:RedirectUri manquants)."
                });

            if (!_tokens.IsAvailable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Le stockage des tokens Gmail n'est pas configuré (Google:TokenEncryptionKey absente ou invalide)."
                });

            return Ok(new GmailConnectStartDto { AuthorizationUrl = _oauth.BuildAuthorizationUrl(CurrentUserId) });
        }

        // GET: api/gmail/callback — cible de redirection de Google.
        // [AllowAnonymous] : l'appel vient de Google, sans cookie JWT ni jeton Bearer.
        // L'identité de l'utilisateur est portée par le state signé (protection anti-CSRF).
        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state, [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
                return Redirect(_oauth.BuildPostCallbackRedirectUrl(false, $"Google a refusé la connexion : {error}"));

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return Redirect(_oauth.BuildPostCallbackRedirectUrl(false, "Paramètres de retour incomplets (code/state)."));

            if (!_tokens.IsAvailable)
                return Redirect(_oauth.BuildPostCallbackRedirectUrl(false, "Le stockage chiffré des tokens Gmail n'est pas configuré sur ce serveur."));

            try
            {
                var connection = await _oauth.HandleCallbackAsync(code, state);
                _logger.LogInformation("Compte Gmail {Address} connecté par l'utilisateur {UserId}.", connection.GmailAddress, connection.UserId);
                return Redirect(_oauth.BuildPostCallbackRedirectUrl(true));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Échec de la connexion Gmail.");
                return Redirect(_oauth.BuildPostCallbackRedirectUrl(false, ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue pendant le callback OAuth Gmail.");
                return Redirect(_oauth.BuildPostCallbackRedirectUrl(false, "Erreur technique pendant la connexion Gmail."));
            }
        }

        // POST: api/gmail/disconnect — révoque le refresh token côté Google puis désactive la connexion
        [HttpPost("disconnect")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<IActionResult> Disconnect()
        {
            var userId = CurrentUserId;
            var connections = await _context.GmailConnections
                .Where(c => c.UserId == userId && c.IsActive)
                .ToListAsync();

            if (connections.Count == 0)
                return NoContent();

            foreach (var connection in connections)
            {
                await _oauth.RevokeAsync(connection);
                connection.IsActive = false;
                connection.DisconnectedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/gmail/sync — rapatrie les nouveaux messages Gmail en base
        [HttpPost("sync")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<GmailSyncResultDto>> Sync([FromQuery] string? query, [FromQuery] int maxResults = 30)
        {
            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            try
            {
                return Ok(await _sync.SyncAsync(connection, query, maxResults, HttpContext.RequestAborted));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
        }

        // ── Messages ─────────────────────────────────────────────────────────

        // GET: api/gmail/messages — liste paginée des messages déjà synchronisés
        [HttpGet("messages")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<ActionResult<GmailMessagePageDto>> GetMessages(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] bool unreadOnly = false,
            [FromQuery] string? search = null)
        {
            var connection = await GetActiveConnectionAsync();
            if (connection == null)
                return Ok(new GmailMessagePageDto { Page = 1, PageSize = pageSize });

            var safePage = Math.Max(1, page);
            var safeSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id);

            if (unreadOnly) query = query.Where(m => !m.IsRead);

            // Recherche plein texte simple sur l'expéditeur, l'objet et l'extrait.
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(m =>
                    (m.From != null && m.From.Contains(term)) ||
                    (m.Subject != null && m.Subject.Contains(term)) ||
                    (m.Snippet != null && m.Snippet.Contains(term)));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(m => m.ReceivedAt)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .Select(m => new GmailMessageListItemDto
                {
                    Id = m.Id,
                    GmailMessageId = m.GmailMessageId,
                    GmailThreadId = m.GmailThreadId,
                    From = m.From,
                    Subject = m.Subject,
                    Snippet = m.Snippet,
                    ReceivedAt = m.ReceivedAt,
                    IsRead = m.IsRead,
                    IsStarred = m.IsStarred,
                    HasAttachments = m.HasAttachments,
                    HasTaskSuggestion = m.Analyses.Any(a => a.IsTask),
                    CreatedTaskId = m.CreatedTaskId
                })
                .ToListAsync();

            return Ok(new GmailMessagePageDto
            {
                Items = items,
                Total = total,
                Page = safePage,
                PageSize = safeSize
            });
        }

        // GET: api/gmail/messages/5 — détail d'un message
        [HttpGet("messages/{id:int}")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<ActionResult<GmailMessageDetailDto>> GetMessage(int id)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            return Ok(new GmailMessageDetailDto
            {
                Id = message.Id,
                GmailMessageId = message.GmailMessageId,
                GmailThreadId = message.GmailThreadId,
                From = message.From,
                To = message.To,
                Cc = message.Cc,
                Subject = message.Subject,
                BodyText = message.BodyText,
                ReceivedAt = message.ReceivedAt,
                IsRead = message.IsRead,
                IsStarred = message.IsStarred,
                CreatedTaskId = message.CreatedTaskId
            });
        }

        // ── Suggestion de tâche par IA ───────────────────────────────────────

        // GET: api/gmail/messages/5/analysis — dernière analyse existante (null si jamais analysé)
        [HttpGet("messages/{id:int}/analysis")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<ActionResult<EmailTaskSuggestionDto>> GetAnalysis(int id)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            var analysis = await _context.EmailAiAnalyses
                .Where(a => a.GmailMessageId == id)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            return Ok(analysis == null ? null : MapAnalysis(analysis, message.CreatedTaskId));
        }

        // POST: api/gmail/messages/5/analysis — demande une analyse IA du message
        [HttpPost("messages/{id:int}/analysis")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<EmailTaskSuggestionDto>> Analyze(int id)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            if (!_ai.IsAvailable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "L'assistance IA n'est pas configurée sur ce serveur (GROQ_API_KEY manquante)."
                });

            TaskSuggestionResult result;
            try
            {
                result = await _ai.AnalyzeForTaskAsync(message.From ?? "", message.Subject, message.BodyText);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            var analysis = new EmailAiAnalysis
            {
                GmailMessageId = message.Id,
                IsTask = result.IsTask,
                Confidence = result.Confidence,
                SuggestedTitle = result.Title,
                SuggestedDescription = result.Description,
                SuggestedPriority = result.Priority,
                SuggestedDueDate = GmailAiService.ParseSuggestedDueDate(result.DueDate),
                SuggestedAssigneeUserId = CurrentUserId,
                AnalysisJson = JsonSerializer.Serialize(result),
                CreatedAt = DateTime.UtcNow,
                Statut = StatutAnalyse.Pending
            };

            _context.EmailAiAnalyses.Add(analysis);
            await _context.SaveChangesAsync();

            return Ok(MapAnalysis(analysis, message.CreatedTaskId));
        }

        // POST: api/gmail/messages/5/analysis/approve — crée la TacheProduction validée par l'utilisateur
        [HttpPost("messages/{id:int}/analysis/approve")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<IActionResult> ApproveAnalysis(int id, CreateTaskFromEmailDto dto)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            if (string.IsNullOrWhiteSpace(dto.Titre))
                return BadRequest(new { message = "Le titre de la tâche est obligatoire." });

            if (message.CreatedTaskId is int existingTaskId)
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    message = "Une tâche a déjà été créée depuis cet email.",
                    taskId = existingTaskId
                });
            }

            var analysis = await _context.EmailAiAnalyses
                .Where(a => a.GmailMessageId == id)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            // ── LOT 16 : la tâche appartient à l'utilisateur IMS qui valide.
            // CreatedByUserId est TOUJOURS l'utilisateur courant : le corps de la requête
            // ne peut pas le définir. L'assignation à un tiers reste possible mais
            // contrôlée (droit PeutAssignerTaches + destinataire existant et actif).
            var currentUserId = CurrentUserId;
            ApplicationUser? assignee;

            if (string.IsNullOrWhiteSpace(dto.AssigneUserId) || dto.AssigneUserId == currentUserId)
            {
                assignee = await _tacheOwnership.FindActiveAssigneeAsync(currentUserId);
            }
            else
            {
                if (!await _permissions.CanAssignerTachesAsync(currentUserId))
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message = "Vous n'êtes pas autorisé à assigner une tâche à un autre utilisateur."
                    });

                assignee = await _tacheOwnership.FindActiveAssigneeAsync(dto.AssigneUserId);
                if (assignee == null)
                    return BadRequest(new { message = "Utilisateur destinataire introuvable ou inactif." });
            }

            var tache = new TacheProduction
            {
                Titre = dto.Titre.Trim(),
                Description = AppendSource(dto.Description, message.From, message.Subject, message.ReceivedAt),
                Priorite = ParsePriority(dto.Priorite),
                DateFinPrevue = dto.DateEcheance,
                Statut = StatutTache.NonCommence,
                DateCreation = DateTime.Now,
                CreePar = CurrentUserName,

                // Propriété décidée côté serveur — jamais depuis le DTO.
                CreatedByUserId = currentUserId
            };

            // Champ legacy aligné sur la FK (source de vérité).
            _tacheOwnership.SetAssignee(tache, assignee);

            _context.TachesProduction.Add(tache);
            await _context.SaveChangesAsync();

            message.CreatedTaskId = tache.Id;
            message.TacheCreee = tache;

            if (analysis != null)
                analysis.Statut = StatutAnalyse.Approved;

            await _context.SaveChangesAsync();

            // Cloche : le responsable désigné est prévenu. Si l'utilisateur qui valide
            // garde la tâche, AUCUNE notification — il vient de la créer, l'information
            // ne lui apporterait rien (cf. LOT 17, pas d'auto-notification).
            await _notifications.NotifierTacheDepuisEmailAsync(
                tache, currentUserId, message.Id, HttpContext.RequestAborted);

            return Ok(new { taskId = tache.Id, titre = tache.Titre });
        }

        // POST: api/gmail/messages/5/analysis/reject — l'utilisateur refuse la suggestion
        [HttpPost("messages/{id:int}/analysis/reject")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<IActionResult> RejectAnalysis(int id)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            if (message.CreatedTaskId is not null)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Une tâche existe déjà pour cet email." });

            var analyses = await _context.EmailAiAnalyses
                .Where(a => a.GmailMessageId == id && a.Statut == StatutAnalyse.Pending)
                .ToListAsync();

            if (analyses.Count == 0) return NoContent();

            foreach (var analysis in analyses)
                analysis.Statut = StatutAnalyse.Rejected;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Réponses IA ──────────────────────────────────────────────────────

        // GET: api/gmail/messages/5/replies
        [HttpGet("messages/{id:int}/replies")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<EmailAiReplyDto>>> GetReplies(int id)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            var replies = await _context.EmailAiReponses
                .Where(r => r.GmailMessageId == id)
                .OrderByDescending(r => r.GeneratedAt)
                .Select(r => MapReply(r))
                .ToListAsync();

            return Ok(replies);
        }

        // POST: api/gmail/messages/5/replies — génère un brouillon de réponse (jamais envoyé)
        [HttpPost("messages/{id:int}/replies")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<EmailAiReplyDto>> GenerateReply(int id, GenerateReplyRequestDto? dto)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            if (!_ai.IsAvailable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "L'assistance IA n'est pas configurée sur ce serveur (GROQ_API_KEY manquante)."
                });

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            string body;
            try
            {
                body = await _ai.GenerateReplyAsync(
                    message.From ?? "", message.Subject, message.BodyText, dto?.Instruction);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            var reply = new EmailAiReply
            {
                GmailMessageId = message.Id,
                Subject = BuildReplySubject(message.Subject),
                Body = string.IsNullOrWhiteSpace(body) ? "Cordialement," : body,
                Statut = StatutReponseIa.Generated,
                GeneratedAt = DateTime.UtcNow
            };

            _context.EmailAiReponses.Add(reply);
            await _context.SaveChangesAsync();

            return Ok(MapReply(reply));
        }

        // PUT: api/gmail/replies/7 — relecture humaine du brouillon
        [HttpPut("replies/{id:int}")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<EmailAiReplyDto>> UpdateReply(int id, UpdateReplyDto dto)
        {
            var reply = await GetOwnedReplyAsync(id);
            if (reply == null) return NotFound(new { message = "Brouillon introuvable." });

            if (reply.Statut == StatutReponseIa.Sent)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Cette réponse a déjà été envoyée." });

            if (string.IsNullOrWhiteSpace(dto.Body))
                return BadRequest(new { message = "Le corps de la réponse est obligatoire." });

            reply.Body = dto.Body;
            if (!string.IsNullOrWhiteSpace(dto.Subject)) reply.Subject = dto.Subject;
            reply.Statut = reply.Statut == StatutReponseIa.Approved ? StatutReponseIa.Approved : StatutReponseIa.Edited;

            await _context.SaveChangesAsync();
            return Ok(MapReply(reply));
        }

        // POST: api/gmail/replies/7/draft — fige la réponse dans un brouillon Gmail
        [HttpPost("replies/{id:int}/draft")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<EmailAiReplyDto>> CreateDraft(int id)
        {
            var reply = await GetOwnedReplyAsync(id);
            if (reply == null) return NotFound(new { message = "Brouillon introuvable." });

            if (reply.Statut == StatutReponseIa.Sent)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Cette réponse a déjà été envoyée." });

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            var message = await _context.GmailMessages.FirstAsync(m => m.Id == reply.GmailMessageId);
            var to = ExtractEmailAddress(message.From);
            if (string.IsNullOrEmpty(to))
                return BadRequest(new { message = "Impossible de déterminer l'adresse du destinataire à partir de l'en-tête From." });

            try
            {
                reply.GmailDraftId = await _gmailApi.CreateDraftAsync(
                    connection,
                    to,
                    reply.Subject ?? BuildReplySubject(message.Subject),
                    reply.Body,
                    message.GmailThreadId,
                    message.Rfc822MessageId);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            reply.Statut = StatutReponseIa.Approved;
            reply.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(MapReply(reply));
        }

        // POST: api/gmail/replies/7/send — envoie le brouillon Gmail (validation humaine explicite)
        [HttpPost("replies/{id:int}/send")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<EmailAiReplyDto>> SendReply(int id)
        {
            var reply = await GetOwnedReplyAsync(id);
            if (reply == null) return NotFound(new { message = "Brouillon introuvable." });

            if (reply.Statut == StatutReponseIa.Sent)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Cette réponse a déjà été envoyée." });

            if (string.IsNullOrEmpty(reply.GmailDraftId))
            {
                // Envoi en un seul geste : on fige d'abord le brouillon Gmail, puis on l'envoie.
                var created = await CreateDraft(id);
                if (created.Result is not OkObjectResult ok || ok.Value is not EmailAiReplyDto draftDto)
                    return created.Result ?? StatusCode(502, new { message = "Création du brouillon Gmail impossible." });

                reply.GmailDraftId = draftDto.GmailDraftId;
            }

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            try
            {
                reply.GmailSentMessageId = await _gmailApi.SendDraftAsync(connection, reply.GmailDraftId!);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            reply.Statut = StatutReponseIa.Sent;
            reply.SentAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(MapReply(reply));
        }

        // POST: api/gmail/replies/7/reject
        [HttpPost("replies/{id:int}/reject")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<IActionResult> RejectReply(int id)
        {
            var reply = await GetOwnedReplyAsync(id);
            if (reply == null) return NotFound(new { message = "Brouillon introuvable." });

            if (reply.Statut == StatutReponseIa.Sent)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Cette réponse a déjà été envoyée." });

            reply.Statut = StatutReponseIa.Rejected;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        // Toute lecture passe par la connexion de l'utilisateur courant : un Id d'email d'un autre
        // utilisateur renvoie 404 (pas de fuite d'existence par un 403).
        private async Task<GmailConnection?> GetActiveConnectionAsync()
        {
            var userId = CurrentUserId;
            return await _context.GmailConnections
                .Where(c => c.UserId == userId && c.IsActive)
                .OrderByDescending(c => c.ConnectedAt)
                .FirstOrDefaultAsync();
        }

        private async Task<GmailMessage?> GetOwnedMessageAsync(int messageId)
        {
            var userId = CurrentUserId;
            return await _context.GmailMessages
                .Include(m => m.GmailConnection)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.GmailConnection.UserId == userId);
        }

        private async Task<EmailAiReply?> GetOwnedReplyAsync(int replyId)
        {
            var userId = CurrentUserId;
            return await _context.EmailAiReponses
                .Include(r => r.GmailMessage)
                .ThenInclude(m => m.GmailConnection)
                .FirstOrDefaultAsync(r => r.Id == replyId && r.GmailMessage.GmailConnection.UserId == userId);
        }

        private static EmailTaskSuggestionDto MapAnalysis(EmailAiAnalysis a, int? createdTaskId) => new()
        {
            Id = a.Id,
            GmailMessageId = a.GmailMessageId,
            IsTask = a.IsTask,
            Confidence = a.Confidence,
            SuggestedTitle = a.SuggestedTitle,
            SuggestedDescription = a.SuggestedDescription,
            SuggestedPriority = a.SuggestedPriority,
            SuggestedDueDate = a.SuggestedDueDate,
            SuggestedAssigneeUserId = a.SuggestedAssigneeUserId,
            Statut = a.Statut.ToString(),
            CreatedTaskId = createdTaskId
        };

        // Projection EF : toutes les propriétés sont translationnelles, pas d'agrégat.
        private static EmailAiReplyDto MapReply(EmailAiReply r) => new()
        {
            Id = r.Id,
            GmailMessageId = r.GmailMessageId,
            Subject = r.Subject,
            Body = r.Body,
            Statut = r.Statut.ToString(),
            GeneratedAt = r.GeneratedAt,
            SentAt = r.SentAt,
            GmailDraftId = r.GmailDraftId
        };

        private static PrioriteTache ParsePriority(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "basse" or "faible" or "1" => PrioriteTache.Basse,
            "haute" or "élevée" or "elevee" or "3" => PrioriteTache.Haute,
            "urgente" or "4" => PrioriteTache.Urgente,
            _ => PrioriteTache.Normale
        };

        private static string BuildReplySubject(string? subject)
        {
            var baseSubject = string.IsNullOrWhiteSpace(subject) ? "Sans objet" : subject.Trim();
            return baseSubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? baseSubject : $"Re: {baseSubject}";
        }

        // « Jean Dupont <jean.dupont@x.com> » -> « jean.dupont@x.com »
        private static string? ExtractEmailAddress(string? from)
        {
            if (string.IsNullOrWhiteSpace(from)) return null;

            var match = Regex.Match(from, "<(?<email>[^>]+)>");
            if (match.Success) return match.Groups["email"].Value.Trim();

            return Regex.IsMatch(from, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") ? from.Trim() : null;
        }

        private static string AppendSource(string? description, string? from, string? subject, DateTime receivedAt)
        {
            var body = string.IsNullOrWhiteSpace(description) ? "" : description.TrimEnd();
            var source = $"\n\n— Créée depuis le courriel « {subject} » reçu de {from} le {receivedAt:dd/MM/yyyy HH:mm}.";

            // TacheProduction.Description est limitée à 1000 caractères.
            const int max = 1000;
            var full = body + source;
            return full.Length <= max ? full : full[..max];
        }
    }
}
