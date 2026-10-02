using System.Linq.Expressions;
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
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<ApplicationUser> _userManager;

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
            UserManager<ApplicationUser> userManager,
            ILogger<GmailController> logger)
        {
            // Réservé à la maintenance administrateur (requalification des pièces jointes,
            // A2) : Testé sur le RÔLE DU DEMANDEUR, pas sur le simple module « courriels »,
            // qui est détenu par des rôles non-administrateurs.
            _userManager = userManager;
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

            // Boîte de réception uniquement : un message archivé ou mis à la corbeille chez
            // Gmail ne doit plus hanter la liste IMS.
            query = query.Where(InInboxFilter);

            if (unreadOnly) query = query.Where(m => !m.IsRead);

            // Insensible à la casse, jokers LIKE échappés, et recherche dans le corps complet
            // (et pas seulement dans l'extrait tronqué par Gmail).
            query = GmailSearch.Apply(query, search, GmailSearch.BuildPattern(search ?? ""));

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
                    // Le drapeau stocké vaut ce que Gmail a répondu AU MOMENT de la
                    // synchronisation : il est faux pour tous les messages importés avant
                    // que le produit ne stocke les pièces. Le rattrapage A2b ne réécrit
                    // aucun message (condition du cahier des charges), donc l'indicateur
                    // est DÉDUIT des pièces réellement présentes. Un EXISTS par message
                    // paginé, pas de jointure ni de double lecture.
                    HasAttachments = m.HasAttachments
                        || _context.GmailAttachments.Any(a => a.GmailMessageId == m.Id && !a.IsInline),
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

            return Ok(await MapDetailAsync(message));
        }

        /// <summary>
        /// PATCH: api/gmail/messages/5 — marque lu/non-lu, étoile, archive, corbeille.
        /// <para>
        /// Gmail est la source de vérité : l'étiquette est modifiée chez Gmail
        /// <b>puis</b> le miroir IMS est mis à jour. L'ordre est délibéré — si Gmail échoue,
        /// l'IMS reste cohérent avec la boîte réelle et l'utilisateur reçoit une erreur
        /// explicite plutôt qu'un état d'interface optimiste qui mentirait. Un échec réseau
        /// ne laisse donc jamais la base désynchronisée.
        /// </para>
        /// </summary>
        [HttpPatch("messages/{id:int}")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<GmailMessageFlagsDto>> UpdateMessageFlags(
            int id, [FromBody] UpdateMessageFlagsDto dto)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            if (!HasUsableConnection(message.GmailConnection))
                return BadRequest(new { message = "Aucun compte Gmail connecté." });

            try
            {
                if (dto.Trash == true)
                {
                    // trash plutôt que modify : Gmail gère seul le retrait de INBOX et
                    // l'ajout de TRASHED, et l'action reste réversible depuis l'interface Gmail.
                    await _gmailApi.TrashMessageAsync(message.GmailConnection, message.GmailMessageId);
                }
                else
                {
                    var (add, remove) = BuildLabelChanges(dto);
                    if (add.Count > 0 || remove.Count > 0)
                        await _gmailApi.ModifyMessageLabelsAsync(message.GmailConnection, message.GmailMessageId, add, remove);
                }
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            ApplyFlagsLocally(message, dto);
            await _context.SaveChangesAsync();

            return Ok(ToFlagsDto(message));
        }

        /// <summary>
        /// PATCH: api/gmail/threads/{gmailThreadId} — mêmes actions, mais sur toute la
        /// conversation en un seul appel Gmail.
        /// <para>
        /// L'interface travaille la conversation comme une unité : « marquer comme lu »
        /// doit concerner les 12 messages d'un fil. Faire 12 appels messages.modify serait
        /// lent et fragile (une coupure réseau au milieu laisserait le fil à moitié traité) ;
        /// <c>threads.modify</c> applique l'étiquette d'un coup, exactement comme Gmail.
        /// </para>
        /// </summary>
        [HttpPatch("threads/{gmailThreadId}")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<GmailThreadFlagsDto>> UpdateThreadFlags(
            string gmailThreadId, [FromBody] UpdateMessageFlagsDto dto)
        {
            if (string.IsNullOrWhiteSpace(gmailThreadId) || gmailThreadId.Length > 255)
                return NotFound(new { message = "Conversation introuvable." });

            var userId = CurrentUserId;

            // L'appartenance du fil est vérifiée par l'existence d'un message à l'utilisateur :
            // un fil qu'il ne connaît pas ne doit rien modifier, chez lui comme chez autrui.
            var owned = await _context.GmailMessages
                .Where(m => m.GmailConnection.UserId == userId && m.GmailThreadId == gmailThreadId)
                .Select(m => new
                {
                    m.GmailConnection,
                    m.GmailConnection.RefreshTokenEncrypted,
                    m.GmailConnection.IsActive
                })
                .FirstOrDefaultAsync();

            if (owned == null) return NotFound(new { message = "Conversation introuvable." });

            if (!owned.IsActive || string.IsNullOrEmpty(owned.RefreshTokenEncrypted))
                return BadRequest(new { message = "Aucun compte Gmail connecté." });

            try
            {
                if (dto.Trash == true)
                {
                    await _gmailApi.TrashThreadAsync(owned.GmailConnection, gmailThreadId);
                }
                else
                {
                    var (add, remove) = BuildLabelChanges(dto);
                    if (add.Count > 0 || remove.Count > 0)
                        await _gmailApi.ModifyThreadLabelsAsync(owned.GmailConnection, gmailThreadId, add, remove);
                }
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            // Miroir local : toutes les lignes du fil, en une seule sauvegarde.
            var messages = await _context.GmailMessages
                .Where(m => m.GmailConnection.UserId == userId && m.GmailThreadId == gmailThreadId)
                .ToListAsync();

            foreach (var message in messages) ApplyFlagsLocally(message, dto);
            await _context.SaveChangesAsync();

            var reference = messages.OrderByDescending(m => m.ReceivedAt).ThenByDescending(m => m.Id).First();
            var flags = ToFlagsDto(reference);

            return Ok(new GmailThreadFlagsDto
            {
                GmailThreadId = gmailThreadId,
                MessageCount = messages.Count,
                IsRead = flags.IsRead,
                IsStarred = flags.IsStarred,
                IsArchived = flags.IsArchived,
                IsTrashed = flags.IsTrashed
            });
        }

        /// <summary>Connexion utilisable : active et porteuse d'un token rafraîchissable.</summary>
        private static bool HasUsableConnection(GmailConnection connection) =>
            connection.IsActive && !string.IsNullOrEmpty(connection.RefreshTokenEncrypted);

        /// <summary>
        /// Traduit la demande utilisateur en étiquettes Gmail. La liste fermée évite qu'un
        /// client n'essaie de poser une étiquette arbitraire (« IMPORTANT », « SPAM »…).
        /// </summary>
        private static (List<string> Add, List<string> Remove) BuildLabelChanges(UpdateMessageFlagsDto dto)
        {
            var add = new List<string>();
            var remove = new List<string>();

            if (dto.IsRead == true) remove.Add("UNREAD");
            if (dto.IsRead == false) add.Add("UNREAD");
            if (dto.IsStarred == true) add.Add("STARRED");
            if (dto.IsStarred == false) remove.Add("STARRED");
            if (dto.Archive == true) remove.Add("INBOX");
            if (dto.Archive == false) add.Add("INBOX");

            return (add, remove);
        }

        /// <summary>
        /// Miroir local des étiquettes Gmail. Appelé APRÈS l'appel à Gmail : si celui-ci
        /// échoue, cette méthode n'est jamais atteinte et la base reste inchangée.
        /// </summary>
        private static void ApplyFlagsLocally(GmailMessage message, UpdateMessageFlagsDto dto)
        {
            var labels = ParseLabels(message.LabelsJson);

            if (dto.Trash == true)
            {
                labels.Remove("INBOX");
                if (!labels.Contains("TRASHED")) labels.Add("TRASHED");
            }
            else if (dto.Archive == true) labels.Remove("INBOX");
            // Remettre en boîte un message qui est à la corbeille n'a pas de sens : on ne
            // rend l'étiquette INBOX que si le message n'est pas déjà à la poubelle.
            else if (dto.Archive == false && !labels.Contains("TRASHED")) labels.Add("INBOX");

            if (dto.IsRead == true) labels.Remove("UNREAD");
            else if (dto.IsRead == false && !labels.Contains("UNREAD")) labels.Add("UNREAD");

            if (dto.IsStarred == true && !labels.Contains("STARRED")) labels.Add("STARRED");
            else if (dto.IsStarred == false) labels.Remove("STARRED");

            message.LabelsJson = JsonSerializer.Serialize(labels);
            if (dto.IsRead.HasValue) message.IsRead = dto.IsRead.Value;
            if (dto.IsStarred.HasValue) message.IsStarred = dto.IsStarred.Value;
        }

        private static GmailMessageFlagsDto ToFlagsDto(GmailMessage message)
        {
            var labels = ParseLabels(message.LabelsJson);
            return new GmailMessageFlagsDto
            {
                Id = message.Id,
                IsRead = message.IsRead,
                IsStarred = message.IsStarred,
                IsArchived = !labels.Contains("INBOX"),
                IsTrashed = labels.Contains("TRASHED")
            };
        }

        /// <summary>
        /// Relit <c>LabelsJson</c>. Une valeur absente ou illisible ne doit pas faire échouer
        /// une action : on repart d'un ensemble vide, ce qui laisse le miroir local moins
        /// précis mais cohérent avec ce que Gmail renvoie à la synchronisation suivante.
        /// </summary>
        private static List<string> ParseLabels(string? labelsJson)
        {
            if (string.IsNullOrWhiteSpace(labelsJson)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(labelsJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Construit le DTO de détail. Le HTML est assaini AU MOMENT DE LA LECTURE (et pas
        /// seulement à la synchronisation) : les messages enregistrés avant l'existence de
        /// l'assainisseur ne seraient jamais repassés par lui.
        /// </summary>
        private async Task<GmailMessageDetailDto> MapDetailAsync(GmailMessage message)
        {
            var attachments = await _context.GmailAttachments
                .Where(a => a.GmailMessageId == message.Id)
                .OrderBy(a => a.IsInline).ThenBy(a => a.FileName)
                .ToListAsync();

            return new GmailMessageDetailDto
            {
                Id = message.Id,
                GmailMessageId = message.GmailMessageId,
                GmailThreadId = message.GmailThreadId,
                From = message.From,
                To = message.To,
                Cc = message.Cc,
                Subject = message.Subject,
                BodyText = message.BodyText,
                BodyHtml = HtmlSanitizer.Sanitize(GmailInlineImageRewriter.Rewrite(message.BodyHtml, message.Id)),
                ReceivedAt = message.ReceivedAt,
                IsRead = message.IsRead,
                IsStarred = message.IsStarred,
                HasAttachments = attachments.Any(a => !a.IsInline),
                Attachments = attachments.Select(a => new GmailAttachmentDto
                {
                    GmailAttachmentId = a.GmailAttachmentId,
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    SizeBytes = a.SizeBytes,
                    IsInline = a.IsInline,
                    Url = $"/api/gmail/messages/{message.Id}/attachments/{Uri.EscapeDataString(a.GmailAttachmentId)}"
                }).ToList(),
                CreatedTaskId = message.CreatedTaskId
            };
        }

        // GET: api/gmail/messages/5/attachments/{attachmentId} — relais binaire depuis Gmail.
        // L'isolation passe par GetOwnedMessageAsync : une pièce d'un email d'un autre
        // utilisateur répond 404, et le contenu n'est JAMAIS mis en cache par un proxy
        // partagé (Cache-Control: private, no-store).
        [HttpGet("messages/{id:int}/attachments/{attachmentId}")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<IActionResult> DownloadAttachment(int id, string attachmentId)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound(new { message = "Message introuvable." });

            if (string.IsNullOrWhiteSpace(attachmentId) || attachmentId.Length > 255)
                return NotFound(new { message = "Pièce jointe introuvable." });

            var attachment = await _context.GmailAttachments
                .FirstOrDefaultAsync(a => a.GmailMessageId == id && a.GmailAttachmentId == attachmentId);

            if (attachment == null) return NotFound(new { message = "Pièce jointe introuvable." });

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            try
            {
                var (_, _, content) = await _gmailApi.GetAttachmentAsync(connection, message.GmailMessageId, attachment.GmailAttachmentId);
                var fileName = attachment.FileName ?? "piece-jointe";
                var mimeType = attachment.MimeType ?? "application/octet-stream";

                return PrivateNoStore(File(content, mimeType, fileName));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
        }

        // GET: api/gmail/messages/5/inline/{contentId} — image intégrée au corps HTML.
        // Servie avec un type MIME strict et sans stockage : un PDF ou un SVG « inline »
        // ne doit jamais être interprété dans le contexte de l'application.
        [HttpGet("messages/{id:int}/inline/{contentId}")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<IActionResult> GetInlineImage(int id, string contentId)
        {
            var message = await GetOwnedMessageAsync(id);
            if (message == null) return NotFound();

            if (string.IsNullOrWhiteSpace(contentId) || contentId.Length > 255)
                return NotFound();

            var attachment = await _context.GmailAttachments
                .FirstOrDefaultAsync(a => a.GmailMessageId == id && a.ContentId == contentId && a.IsInline);

            if (attachment == null) return NotFound();

            // Un « inline » qui n'est pas une image raster est refusé : le proxy ne doit
            // pas devenir un vecteur de rendu de contenu arbitraire dans l'origine IMS.
            var mimeType = attachment.MimeType ?? "";
            if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            if (mimeType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase))
            {
                // Un SVG est un document actif (script) : il ne peut pas être servi tel quel
                // dans une page, même encodé en base64.
                return NotFound();
            }

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return NotFound();

            try
            {
                var (_, _, content) = await _gmailApi.GetAttachmentAsync(connection, message.GmailMessageId, attachment.GmailAttachmentId);
                return PrivateNoStore(File(content, mimeType));
            }
            catch (InvalidOperationException)
            {
                // Une image cassée ne doit pas faire échouer le rendu de l'email.
                return NotFound();
            }
        }

        /// <summary>
        /// Interdit la mise en cache d'un contenu relé depuis Gmail.
        /// <para>
        /// Une pièce jointe ou une image intégrée est un octet privé appartenant à
        /// l'utilisateur : la conserver dans un cache partagé (CDN, proxy d'entreprise) la
        /// rendrait lisible par le service après expiration, et la res servirait ensuite à
        /// un tiers non autorisé sur la même URL. <c>private, no-store</c> ferme les deux risques.
        /// </para>
        /// </summary>
        private IActionResult PrivateNoStore(IActionResult result)
        {
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.Pragma = "no-cache";
            return result;
        }

        /// <summary>
        /// Filtre « boîte de réception » appliqué en base.
        /// <para>
        /// Un <c>LabelsJson</c> nul vient d'un email synchronisé avant le stockage des
        /// étiquettes : il est traité comme présent dans la boîte de réception plutôt que
        /// filtré, sinon tous les emails historiques disparaîtraient de la liste.
        /// </para>
        /// </summary>
        private static readonly Expression<Func<GmailMessage, bool>> InInboxFilter =
            m => m.LabelsJson == null || EF.Functions.ILike(m.LabelsJson, "%\"INBOX\"%");


        // ── Fils de discussion ───────────────────────────────────────────────

        // GET: api/gmail/threads — UN fil par ligne, positionné sur son message le plus récent.
        // Sans ce regroupement, une discussion de 12 messages occupait 12 lignes de la liste
        // et l'utilisateur n'en voyait qu'une, la plus ancienne en haut de l'extrait.
        [HttpGet("threads")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<ActionResult<GmailThreadPageDto>> GetThreads(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
            [FromQuery] bool unreadOnly = false, [FromQuery] string? search = null)
        {
            var connection = await GetActiveConnectionAsync();
            if (connection == null)
                return Ok(new GmailThreadPageDto { Page = 1, PageSize = pageSize });

            var safePage = Math.Max(1, page);
            var safeSize = Math.Clamp(pageSize, 1, 100);

            var scope = _context.GmailMessages.Where(m => m.GmailConnectionId == connection.Id);
            // Boîte de réception uniquement, comme pour la liste de messages : un fil dont
            // tous les messages sont archivés ou à la corbeille ne doit plus occuper de ligne.
            scope = scope.Where(InInboxFilter);
            if (unreadOnly) scope = scope.Where(m => !m.IsRead);
            scope = GmailSearch.Apply(scope, search, GmailSearch.BuildPattern(search ?? ""));

            // Le total compte des FILS, pas des messages : c'est ce que l'interface pagine.
            var total = await scope.Select(m => m.GmailThreadId).Distinct().CountAsync();

            // Le « dernier message du fil » est le plus récent de chaque GmailThreadId.
            // On l'obtient par GROUP BY sur la date maximale (les timestamps Gmail sont en
            // millisecondes : les égalités sont théoriques, et départagées côté client par
            // l'Id). Éviter g.OrderBy().First() dans la projection : Npgsql ne le traduit pas.
            var pagePairs = await scope
                .GroupBy(m => m.GmailThreadId)
                .Select(g => new { GmailThreadId = g.Key, ReceivedAt = g.Max(m => m.ReceivedAt) })
                .OrderByDescending(x => x.ReceivedAt)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .ToListAsync();

            if (pagePairs.Count == 0)
                return Ok(new GmailThreadPageDto { Total = total, Page = safePage, PageSize = safeSize });

            // Toutes les lignes des fils de la page (page ≤ 100, les agrégations se font en
            // mémoire) : on évite ainsi les projections GROUP BY complexes mal traduites.
            var pageThreadIds = pagePairs.Select(p => p.GmailThreadId).ToList();
            var pageMessages = await _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id && pageThreadIds.Contains(m.GmailThreadId))
                .Select(m => new
                {
                    m.Id, m.GmailThreadId, m.GmailMessageId, m.Subject, m.Snippet, m.ReceivedAt, m.IsRead,
                    m.IsStarred, m.HasAttachments, m.From,
                    HasTaskSuggestion = m.Analyses.Any(a => a.IsTask)
                })
                .ToListAsync();

            // Trombone des fils : le drapeau stocké, MIS EN OR avec les pièces réellement
            // présentes. Le drapeau vaut ce que Gmail a répondu au moment de la
            // synchronisation — il est donc faux pour tout message importé avant que le
            // produit ne stocke les pièces, y compris après le rattrapage A2b, qui ne
            // réécrit aucun message. Une seule requête, bornée par les lignes déjà lues
            // ci-dessus : ni jointure, ni seconde lecture de la table des messages.
            var pageMessageIds = pageMessages.Select(m => m.Id).ToList();
            var idsAvecPiece = (await _context.GmailAttachments
                    .Where(a => !a.IsInline && pageMessageIds.Contains(a.GmailMessageId))
                    .Select(a => a.GmailMessageId)
                    .ToListAsync())
                .ToHashSet();

            // Regroupement et comptage client-side : le dernier message du fil reste celui
            // dont (ReceivedAt, Id) est maximal, dragage des égalités de date.
            // Suivi et pièces jointes ne sont PAS lus sur ce dernier message : Gmail rattache
            // l'étoile et le trombone au FIL, c'est-à-dire à l'un de ses messages. Lire
            // l'état du dernier message ferait disparaître le suivi dès qu'un tiers répond
            // (cas réel : on suit le premier email, la client répond, l'icône s'éteint) et
            // masquerait la pièce jointe d'un message plus ancien.
            var byThread = pageMessages
                .GroupBy(m => m.GmailThreadId)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Last: g.OrderByDescending(m => m.ReceivedAt).ThenByDescending(m => m.Id).First(),
                        Count: g.Count(),
                        Unread: g.Count(m => !m.IsRead),
                        IsStarred: g.Any(m => m.IsStarred),
                        HasAttachments: g.Any(m => m.HasAttachments || idsAvecPiece.Contains(m.Id)),
                        HasSuggestion: g.Any(m => m.HasTaskSuggestion),
                        Participants: g.Select(m => m.From).Where(f => f != null).Select(f => f!).Distinct().ToList()));

            var items = pagePairs.Select(p =>
            {
                var (last, count, unread, isStarred, hasAttachments, hasSuggestion, participants) = byThread[p.GmailThreadId];
                return new GmailThreadListItemDto
                {
                    GmailThreadId = p.GmailThreadId,
                    Subject = last.Subject,
                    Snippet = last.Snippet,
                    LastMessageId = last.Id,
                    LastGmailMessageId = last.GmailMessageId,
                    LastMessageAt = last.ReceivedAt,
                    MessageCount = count,
                    UnreadCount = unread,
                    IsStarred = isStarred,
                    HasAttachments = hasAttachments,
                    HasTaskSuggestion = hasSuggestion,
                    Participants = participants
                };
            }).ToList();

            return Ok(new GmailThreadPageDto { Items = items, Total = total, Page = safePage, PageSize = safeSize });
        }

        // GET: api/gmail/threads/{gmailThreadId} — conversation complète, du plus ancien au plus récent.
        // L'isolation passe par GmailConnectionId : un identifiant de fil devin par un autre
        // utilisateur ne doit rien révéler, il doit répondre 404.
        [HttpGet("threads/{gmailThreadId}")]
        [RequireModulePermission("courriels", requireWrite: false)]
        public async Task<ActionResult<GmailThreadDetailDto>> GetThread(string gmailThreadId)
        {
            if (string.IsNullOrWhiteSpace(gmailThreadId) || gmailThreadId.Length > 255)
                return NotFound(new { message = "Fil introuvable." });

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return NotFound(new { message = "Fil introuvable." });

            var rows = await _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id && m.GmailThreadId == gmailThreadId)
                .OrderBy(m => m.ReceivedAt).ThenBy(m => m.Id)
                .Select(m => new { m.Id })
                .ToListAsync();

            if (rows.Count == 0) return NotFound(new { message = "Fil introuvable." });

            // Réutilise le mapping de détail (assainissement HTML + pièces jointes) plutôt
            // que de le dupliquer : deux chemins de projection divergeraient vite.
            var entities = await _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id && m.GmailThreadId == gmailThreadId)
                .OrderBy(m => m.ReceivedAt).ThenBy(m => m.Id)
                .ToListAsync();

            var messages = new List<GmailMessageDetailDto>(entities.Count);
            foreach (var entity in entities)
                messages.Add(await MapDetailAsync(entity));

            return Ok(new GmailThreadDetailDto
            {
                GmailThreadId = gmailThreadId,
                Subject = messages[^1].Subject,
                Messages = messages
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
            catch (Exception ex)
            {
                // Filet de sécurité : une défaillance inattendue du service IA ne doit JAMAIS
                // remonter en 500 avec la stack trace dans le corps de la réponse. Le message
                // au client reste générique ; le détail va au journal, sans clé ni corps d'email.
                _logger.LogError(ex, "Échec inattendu de l'analyse IA du message {MessageId}.", id);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "L'assistance IA n'a pas pu analyser cet email. Réessayez dans un instant."
                });
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
        // Le corps et l'objet reçus font foi : c'est le texte affiché à l'écran, pas
        // l'entité relue en base. Sans cela, une modification non enregistrée disparaissait
        // à l'envoi — perte silencieuse, l'API renvoyant un succès.
        [HttpPost("replies/{id:int}/send")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<EmailAiReplyDto>> SendReply(int id, [FromBody] SendReplyDto dto)
        {
            var reply = await GetOwnedReplyAsync(id);
            if (reply == null) return NotFound(new { message = "Brouillon introuvable." });

            if (reply.Statut == StatutReponseIa.Sent)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Cette réponse a déjà été envoyée." });

            if (string.IsNullOrWhiteSpace(dto.Body))
                return BadRequest(new { message = "Le corps de la réponse est obligatoire." });

            // Même validation et même plafond que la composition d'un nouveau message
            // (DecodeAttachments, extrait commun) : une réponse ne doit pas pouvoir
            // envoyer ce qu'un nouveau message refuse.
            var decoded = DecodeAttachments(dto.Attachments);
            if (!decoded.Ok)
            {
                return decoded.TropGros
                    ? StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = decoded.Erreur })
                    : BadRequest(new { message = decoded.Erreur });
            }

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            var message = await _context.GmailMessages.FirstAsync(m => m.Id == reply.GmailMessageId);
            var to = ExtractEmailAddress(message.From);
            if (string.IsNullOrEmpty(to))
                return BadRequest(new { message = "Impossible de déterminer l'adresse du destinataire à partir de l'en-tête From." });

            var subject = string.IsNullOrWhiteSpace(dto.Subject)
                ? reply.Subject ?? BuildReplySubject(message.Subject)
                : dto.Subject;

            // PERSISTANCE AVANT toute action Gmail. L'historique du brouillon doit refléter
            // ce qui a été envoyé : si l'envoi échoue, l'utilisateur a bien modifié sa réponse,
            // et repartir d'un texte périmé au second essai serait une seconde perte.
            reply.Body = dto.Body;
            reply.Subject = subject;
            reply.Statut = reply.Statut == StatutReponseIa.Approved ? StatutReponseIa.Approved : StatutReponseIa.Edited;
            await _context.SaveChangesAsync();

            try
            {
                var pieces = (IReadOnlyList<(string FileName, string MimeType, byte[] Content)>)decoded.Pieces;

                if (string.IsNullOrEmpty(reply.GmailDraftId))
                {
                    reply.GmailDraftId = await _gmailApi.CreateDraftAsync(
                        connection, to, subject, reply.Body,
                        message.GmailThreadId, message.Rfc822MessageId, pieces);
                }
                else
                {
                    // Un brouillon existe déjà : il porte le texte d'une version antérieure.
                    // On le RÉÉCRIT avant d'envoyer, sinon c'est ce texte-là qui part.
                    await _gmailApi.UpdateDraftAsync(
                        connection, reply.GmailDraftId, to, subject, reply.Body,
                        message.GmailThreadId, message.Rfc822MessageId, pieces);
                }

                reply.GmailSentMessageId = await _gmailApi.SendDraftAsync(connection, reply.GmailDraftId!);
            }
            catch (InvalidOperationException ex)
            {
                await _context.SaveChangesAsync();
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            // Le statut « envoyé » n'est positionné qu'après le succès de Gmail : le
            // brouillon reste alors rejouable si l'appel a échoué.
            reply.Statut = StatutReponseIa.Sent;
            reply.SentAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(MapReply(reply));
        }

        // POST: api/gmail/replies/7/reject
        // Refuser = « je ne veux pas de cette réponse ». Si un brouillon a déjà été figé
        // dans Gmail, il doit disparaître de la boîte de réception de l'utilisateur :
        // le laisser produirait exactement le déchet que l'utilisateur vient de refuser.
        [HttpPost("replies/{id:int}/reject")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<IActionResult> RejectReply(int id)
        {
            var reply = await GetOwnedReplyAsync(id);
            if (reply == null) return NotFound(new { message = "Brouillon introuvable." });

            if (reply.Statut == StatutReponseIa.Sent)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Cette réponse a déjà été envoyée." });

            if (!string.IsNullOrEmpty(reply.GmailDraftId))
            {
                var connection = await GetActiveConnectionAsync();
                if (connection == null)
                {
                    // Sans connexion active on ne peut pas appeler Gmail. On ne fige surtout
                    // pas un « Refusé » : la décision utilisateur n'est pas appliquée.
                    return BadRequest(new { message = "Aucun compte Gmail connecté : le brouillon Gmail ne peut pas être supprimé." });
                }

                try
                {
                    await _gmailApi.DeleteDraftAsync(connection, reply.GmailDraftId!);
                }
                catch (InvalidOperationException ex)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
                }

                // L'identifiant n'est plus valide : le conserver ferait rejouer un 404 à chaque action.
                reply.GmailDraftId = null;
            }

            reply.Statut = StatutReponseIa.Rejected;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/gmail/drafts/edit — reformulation ou traduction du texte saisi par
        // l'utilisateur. Endpoint sans état : AUCUN enregistrement n'est créé ni modifié,
        // le résultat est renvoyé pour remplacer le contenu de la zone de composition.
        [HttpPost("drafts/edit")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<EditDraftResponseDto>> EditDraft([FromBody] EditDraftRequestDto dto)
        {
            // Validation AVANT disponibilité : une requête invalide (mauvaise langue, mauvaise
            // action) doit être rejetée 400 que l'IA soit up ou down — le 503 ne concerne
            // que des requêtes valides qui n'ont pas pu être traitées.
            if (!Enum.TryParse<DraftEditAction>(dto.Action, ignoreCase: true, out var action)
                || !Enum.IsDefined(action))
            {
                return BadRequest(new { message = "Action inconnue. Utilisez « Rewrite » ou « Translate »." });
            }

            if (string.IsNullOrWhiteSpace(dto.Text))
                return BadRequest(new { message = "Le texte à reformuler est vide." });

            if (action == DraftEditAction.Translate && !TranslateLanguage.IsSupported(dto.TargetLanguage))
            {
                return BadRequest(new
                {
                    message = "Langue non prise en charge. Valeurs autorisées : FR, EN, AR."
                });
            }

            if (!_ai.IsAvailable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "L'assistance IA n'est pas configurée sur ce serveur (GROQ_API_KEY manquante)."
                });

            try
            {
                var text = await _ai.EditDraftTextAsync(action, dto.Text, dto.Instruction, dto.TargetLanguage);
                return Ok(new EditDraftResponseDto
                {
                    Text = text,
                    Action = action.ToString(),
                    TargetLanguage = action == DraftEditAction.Translate ? dto.TargetLanguage!.Trim().ToUpperInvariant() : null
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec inattendu de l'édition IA du brouillon.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "L'assistance IA n'a pas pu traiter ce texte. Réessayez dans un instant."
                });
            }
        }

        /// <summary>
        /// Valide et décode les pièces jointes d'un envoi. Partagé par la composition d'un
        /// nouveau message et par la réponse à un email (A4) : les deux doivent appliquer
        /// EXACTEMENT le même plafond, sinon la réponse autoriserait ce que la composition
        /// refuse — et l'écart se découvrirait à l'envoi, sur le message le plus sensible.
        /// </summary>
        private (bool Ok, bool TropGros, string? Erreur,
            List<(string FileName, string MimeType, byte[] Content)> Pieces, long TotalBytes)
            DecodeAttachments(List<ComposeAttachmentDto>? incoming)
        {
            var pieces = new List<(string, string, byte[])>();
            long totalBytes = 0;

            foreach (var attachment in incoming ?? new List<ComposeAttachmentDto>())
            {
                byte[] content;
                try
                {
                    content = Convert.FromBase64String(attachment.ContentBase64);
                }
                catch (FormatException)
                {
                    return (false, false,
                        $"Le contenu de « {attachment.FileName} » n'est pas du Base64 valide.",
                        pieces, totalBytes);
                }

                totalBytes += content.LongLength;

                // Plafond appliqué AVANT l'envoi : c'est le serveur qui décide. Une
                // validation uniquement côté navigateur laisserait passer un appel direct.
                if (totalBytes > MaxComposeAttachmentBytes)
                {
                    return (false, true,
                        $"Les pièces jointes dépassent la limite de {MaxComposeAttachmentBytes / (1024 * 1024)} Mo ({totalBytes / (1024 * 1024.0):F1} Mo envoyés).",
                        pieces, totalBytes);
                }

                pieces.Add((
                    string.IsNullOrWhiteSpace(attachment.FileName) ? "piece-jointe" : attachment.FileName,
                    string.IsNullOrWhiteSpace(attachment.MimeType) ? "application/octet-stream" : attachment.MimeType,
                    content));
            }

            return (true, false, null, pieces, totalBytes);
        }


        // POST: api/gmail/send — compose et envoie un email via la boîte Gmail connectée.
        // Ne crée AUCUNE ligne en base : ni EmailAiReply, ni EmailAiAnalysis. La composition
        // est un brouillon éphémère ; seul Gmail conserve l'envoi.
        [HttpPost("send")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<ActionResult<ComposeEmailResultDto>> Send([FromBody] ComposeEmailDto dto)
        {
            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            var to = NormalizeAddresses(dto.To);
            if (to.Count == 0)
                return BadRequest(new { message = "Au moins un destinataire est obligatoire." });

            var cc = NormalizeAddresses(dto.Cc);
            var bcc = NormalizeAddresses(dto.Bcc);

            if (string.IsNullOrWhiteSpace(dto.BodyText) && string.IsNullOrWhiteSpace(dto.BodyHtml))
                return BadRequest(new { message = "Le message est vide." });

            var attachments = new List<(string FileName, string MimeType, byte[] Content)>();
            long totalBytes = 0;

            var decoded = DecodeAttachments(dto.Attachments);
            if (!decoded.Ok)
            {
                return decoded.TropGros
                    ? StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = decoded.Erreur })
                    : BadRequest(new { message = decoded.Erreur });
            }

            attachments.AddRange(decoded.Pieces);
            totalBytes = decoded.TotalBytes;

            // Le fil de réponse est résolu côté IMS : on n'accepte qu'un GmailMessageId
            // appartenant à l'utilisateur, jamais un identifiant arbitraire fourni par le client.
            string? threadId = null;
            string? inReplyTo = null;
            if (!string.IsNullOrWhiteSpace(dto.InReplyTo))
            {
                var parent = await _context.GmailMessages
                    .Where(m => m.GmailConnectionId == connection.Id && m.GmailMessageId == dto.InReplyTo)
                    .Select(m => new { m.GmailThreadId, m.Rfc822MessageId })
                    .FirstOrDefaultAsync();

                if (parent == null)
                    return BadRequest(new { message = "Le message auquel vous répondez est introuvable." });

                threadId = parent.GmailThreadId;
                inReplyTo = parent.Rfc822MessageId;
            }

            try
            {
                var (messageId, sentThreadId) = await _gmailApi.SendMessageAsync(
                    connection,
                    string.Join(", ", to),
                    cc.Count > 0 ? string.Join(", ", cc) : null,
                    bcc.Count > 0 ? string.Join(", ", bcc) : null,
                    dto.Subject ?? "(sans objet)",
                    dto.BodyText,
                    dto.BodyHtml,
                    threadId,
                    inReplyTo,
                    attachments);

                return Ok(new ComposeEmailResultDto
                {
                    GmailMessageId = messageId,
                    GmailThreadId = sentThreadId,
                    AttachmentCount = attachments.Count,
                    TotalBytes = totalBytes
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Plafond IMS des pièces jointes d'un envoi : 20 Mo cumulés.
        /// <para>
        /// La limite Gmail API est de 35 Mo, mais on ne s'y colle pas : au-delà d'environ
        /// 25 Mo, Gmail convertit le message en lien de téléchargement et l'historique du fil
        /// devient pénible à lire. 20 Mo couvre largement un bon de commande ou un plan de
        /// coupe, en laissant une marge confortable pour l'encodage Base64 (+33 %).
        /// </para>
        /// </summary>
        public const int MaxComposeAttachmentBytes = 20 * 1024 * 1024;

        // ── Maintenance A2 : reclasser les pièces jointes déjà stockées ──────────
        //
        // Les messages synchronisés AVANT la correction « inline vs attachment » ont
        // leurs pièces classées avec l'ancien critère (présence d'un Content-ID). Ce
        // endpoint les re-qualifie à partir de Gmail. Il est :
        //   - réservé aux administrateurs (relance enlot sur un compte client) ;
        //   - JAMAIS automatique (aucun appel au démarrage ni à la synchronisation) ;
        //   - idempotent (relancer ne change plus rien) ;
        //   - borné par lot, avec une pause entre deux lots pour respecter les quotas ;
        //   - strictement limité aux PIÈCES JOINTES : aucun message n'est modifié.

        /// <summary>Taille maximale d'un lot : borne la durée HTTP et la mémoire.</summary>
        public const int AttachmentQualifierMaxBatch = 50;

        /// <summary>
        /// Budget de messages relus par défaut. Volontairement bas : l'endpoint est
        /// idempotent, donc l'administrateur le relance autant de fois qu'il faut plutôt
        /// que de faire tenir 2 000 lectures dans une seule requête HTTP.
        /// </summary>
        public const int AttachmentQualifierDefaultBudget = 50;

        /// <summary>
        /// Budget maximal par exécution. L'API Gmail recommande au plus 250 requêtes par
        /// utilisateur et par fenêtre glissante ; on reste sous ce plafond au lieu de
        /// découvrir un 429 en plein traitement.
        /// </summary>
        public const int AttachmentQualifierMaxBudget = 250;

        /// <summary>
        /// Intervalle minimal entre deux lectures. 250 requêtes / 100 s = 400 ms : c'est
        /// la seule façon de tenir le plafond sans dépendre d'un 429, chaque appel
        /// consommant 5 unités de quota.
        /// </summary>
        private const int AttachmentQualifierMinIntervalMs = 400;

        /// <summary>
        /// Nombre maximal de pages « messages.list » parcourues pour recenser les messages
        /// porteurs d'une pièce jointe. Une page = 100 identifiants pour 5 unités de quota,
        /// contre 5 unités par message relu : recenser est donc 20 fois moins cher que
        /// vérifier. Le plafond évite qu'un dossier Gmail de plusieurs dizaines de milliers
        /// de messages fasse tourner une page HTTP pendant des minutes.
        /// </summary>
        private const int AttachmentBackfillMaxListPages = 10;

        /// <summary>Requête Gmail qui recense les messages porteurs d'une pièce jointe.</summary>
        private const string GmailHasAttachmentQuery = "has:attachment";

        [HttpPost("maintenance/attachments/requalify")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<IActionResult> RequalifyAttachments(
            [FromQuery] int? batchSize,
            [FromQuery] int? maxMessages)
        {
            var garde = await GardeMaintenanceAttachmentsAsync("la requalification des pièces jointes");
            if (garde != null) return garde;

            var connection = await GetActiveConnectionAsync();
            if (connection == null)
                return BadRequest(new { message = "Aucun compte Gmail connecté." });

            var batch = Math.Clamp(batchSize ?? AttachmentQualifierMaxBatch, 1, AttachmentQualifierMaxBatch);
            var budget = Math.Clamp(
                maxMessages ?? AttachmentQualifierDefaultBudget, 1, AttachmentQualifierMaxBudget);

            // Seuls les messages ayant au moins une pièce peuvent être concernés, et
            // l'ordre est stable : une relance avec le même budget relit les mêmes
            // messages, ce qui rend l'opération vérifiable (et sans effet de bord).
            var candidats = await _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id)
                .Where(m => _context.GmailAttachments.Any(a => a.GmailMessageId == m.Id))
                .OrderBy(m => m.Id)
                .Select(m => m.Id)
                .Take(budget)
                .ToListAsync();

            if (candidats.Count == 0)
                return Ok(new AttachmentRequalificationDto
                {
                    Examines = 0,
                    Reclasses = 0,
                    Erreurs = 0,
                    Messages = new List<string>()
                });

            var traites = 0;
            var reclasses = 0;
            var erreurs = 0;
            var details = new List<string>();

            for (var offset = 0; offset < candidats.Count; offset += batch)
            {
                foreach (var messageId in candidats.Skip(offset).Take(batch))
                {
                    if (HttpContext.RequestAborted.IsCancellationRequested)
                        return Ok(new AttachmentRequalificationDto
                        {
                            Examines = traites,
                            Reclasses = reclasses,
                            Erreurs = erreurs,
                            Messages = details
                        });

                    if (traites > 0)
                    {
                        // Une lecture par message, quel que soit le découpage en lots :
                        // c'est le rythme qui protège le quota, pas la taille du lot. Le délai
                        // est entre chaque appel (et non entre deux lots) pour qu'un
                        // budget de 250 ne parte pas en 5 rafales de 50.
                        await Task.Delay(AttachmentQualifierMinIntervalMs, HttpContext.RequestAborted);
                    }

                    traites++;
                    try
                    {
                        var message = await _context.GmailMessages
                            .FirstAsync(m => m.Id == messageId, HttpContext.RequestAborted);

                        // Lecture COMPLÈTE, volontairement : la qualification « inline »
                        // exige de savoir quels cid: le corps référence. Une lecture
                        // « metadata » coûterait le MÊME quota (5 unités) tout en étant
                        // incapable d'appliquer la règle : elle ne voit ni le corps, ni
                        // de façon fiable les en-têtes de chaque partie.
                        var apiParts = (await _gmailApi.GetMessageAsync(connection, message.GmailMessageId)).Attachments;
                        var existantes = await _context.GmailAttachments
                            .Where(a => a.GmailMessageId == messageId)
                            .ToListAsync();

                        var parId = apiParts.ToDictionary(p => p.GmailAttachmentId, StringComparer.Ordinal);
                        var changes = 0;

                        foreach (var existante in existantes)
                        {
                            if (!parId.TryGetValue(existante.GmailAttachmentId, out var api))
                            {
                                // La pièce a disparu de Gmail (ou l'identifiant a changé) :
                                // on ne la supprime pas, la métadonnée reste un historique
                                // de ce qui a été reçu. Seule la qualification est dans le
                                // périmètre de cette maintenance.
                                continue;
                            }

                            if (existante.IsInline == api.IsInline
                                && existante.ContentId == api.ContentId)
                                continue;

                            existante.IsInline = api.IsInline;
                            existante.ContentId = api.ContentId;
                            changes++;
                        }

                        if (changes > 0)
                        {
                            await _context.SaveChangesAsync(HttpContext.RequestAborted);
                            reclasses += changes;
                            details.Add($"Message {messageId} : {changes} pièce(s) reclassée(s).");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Un message en erreur (supprimé côté Gmail, quota, réseau) ne doit
                        // pas interrompre la requalification : on le compte et on continue.
                        erreurs++;
                        _logger.LogWarning(
                            ex, "Requalification A2 : message {MessageId} ignoré", messageId);
                        details.Add($"Message {messageId} : ignoré ({ex.GetType().Name}).");
                    }
                }
            }

            return Ok(new AttachmentRequalificationDto
            {
                Examines = traites,
                Reclasses = reclasses,
                Erreurs = erreurs,
                Messages = details
            });
        }

        // Les messages antérieurs à l'import des pièces jointes n'ont AUCUNE ligne en base :
        // le trombone n'apparaît donc pas, et rien dans le produit ne les rattrape tout seul.
        // Cet endpoint crée ces lignes manquantes — la qualification A2
        // (Content-Disposition / cid: référencé) est appliquée À LA CRÉATION, puisqu'elle
        // sort du parseur : rien à reclasser sur une ligne qui n'existait pas. Les lignes
        // DÉJÀ présentes, elles, sont traitées par la requalification ci-dessus.
        // Mêmes garanties : administrateur seul, jamais automatique, idempotent, borné par
        // lot avec pause entre deux lectures, et AUCUN message modifié.

        [HttpPost("maintenance/attachments/rattrapage")]
        [RequireModulePermission("courriels", requireWrite: true)]
        public async Task<IActionResult> BackfillAttachments(
            [FromQuery] int? batchSize,
            [FromQuery] int? maxMessages)
        {
            var garde = await GardeMaintenanceAttachmentsAsync("le rattrapage des pièces jointes");
            if (garde != null) return garde;

            var connection = await GetActiveConnectionAsync();
            if (connection == null) return BadRequest(new { message = "Aucun compte Gmail connecté." });

            var batch = Math.Clamp(batchSize ?? AttachmentQualifierMaxBatch, 1, AttachmentQualifierMaxBatch);
            var budget = Math.Clamp(
                maxMessages ?? AttachmentQualifierDefaultBudget, 1, AttachmentQualifierMaxBudget);

            // Recenser les messages que Gmail déclare porteurs d'une pièce jointe coûte
            // 5 unités par tranche de 100 identifiants, quand une lecture complète en coûte
            // 5 pour UN message. Sans ce filtre, il faudrait relire toute la boîte pour
            // découvrir que la plupart des messages n'ont rien — et les messages réellement
            // sans pièce resteraient candidats éternellement, à chaque relance.
            // En cas d'échec du recensement, on continue sans filtre plutôt que de ne rien
            // faire du tout : une maintenance déclenchée à la main ne doit pas rendre les
            // bras écartés en silence, et le budget borne de toute façon la facture.
            var idsAvecPieces = new HashSet<string>(StringComparer.Ordinal);
            var recensementEchoue = false;
            try
            {
                string? pageToken = null;
                for (var page = 0; page < AttachmentBackfillMaxListPages; page++)
                {
                    var (ids, next) = await _gmailApi.ListMessageIdsPageAsync(
                        connection, GmailHasAttachmentQuery, 100, pageToken);
                    foreach (var id in ids) idsAvecPieces.Add(id);

                    if (string.IsNullOrEmpty(next)) break;
                    pageToken = next;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                recensementEchoue = true;
                _logger.LogWarning(ex, "Rattrapage A2b : recensement « has:attachment » indisponible");
            }

            // Candidats : messages synchronisés de cette connexion, sans AUCUNE ligne de
            // pièce jointe, et — si le recensement a abouti — déclarés porteurs d'une pièce
            // par Gmail. Ordre stable par Id : la reprise est donc naturelle, un message
            // rattrapé quittant la liste pour de bon.
            IQueryable<GmailMessage> query = _context.GmailMessages
                .Where(m => m.GmailConnectionId == connection.Id && m.IsSynchronized)
                .Where(m => !_context.GmailAttachments.Any(a => a.GmailMessageId == m.Id));

            if (!recensementEchoue)
                query = query.Where(m => idsAvecPieces.Contains(m.GmailMessageId));

            // Ordre stable par Id : la reprise est naturelle, un message rattrappé quittant
            // la liste pour de bon.
            query = query.OrderBy(m => m.Id);

            var candidats = await query
                .Select(m => new { m.Id, m.GmailMessageId })
                .Take(budget + 1)
                .ToListAsync(HttpContext.RequestAborted);

            // Un message de plus que le budget : c'est la preuve qu'il reste du travail,
            // et c'est ce qui permet à l'interface d'afficher « relancez ».
            var restants = candidats.Count > budget ? candidats.Count - budget : 0;
            if (restants > 0) candidats.RemoveAt(candidats.Count - 1);

            if (candidats.Count == 0)
                return Ok(new AttachmentBackfillDto
                {
                    Restants = 0,
                    Messages = new List<string>()
                });

            var traites = 0;
            var creees = 0;
            var erreurs = 0;
            var details = new List<string>();

            for (var offset = 0; offset < candidats.Count; offset += batch)
            {
                foreach (var candidat in candidats.Skip(offset).Take(batch))
                {
                    if (HttpContext.RequestAborted.IsCancellationRequested)
                        return Ok(BuildBackfillReport(traites, creees, erreurs, restants, details));

                    if (traites > 0)
                    {
                        // Une lecture par message, comme en A2 : c'est le rythme qui protège
                        // le quota, pas la taille du lot.
                        await Task.Delay(AttachmentQualifierMinIntervalMs, HttpContext.RequestAborted);
                    }

                    traites++;
                    try
                    {
                        // Lecture COMPLÈTE, pour la même raison qu'en A2 : la qualification
                        // « inline » exige de savoir quels cid: le corps référence. C'est le
                        // SEUL appel qui consomme du quota Gmail par message.
                        var apiParts = (await _gmailApi.GetMessageAsync(connection, candidat.GmailMessageId))
                            .Attachments;

                        // Le candidat a été choisi SANS aucune ligne : tout ce que Gmail
                        // renvoie est donc à créer. On relit la table avant d'écrire
                        // malgré tout — une maintenance lancée en parallèle ne doit pas
                        // violer l'unicité (GmailMessageId, GmailAttachmentId).
                        var dejaEnBase = await _context.GmailAttachments
                            .Where(a => a.GmailMessageId == candidat.Id)
                            .Select(a => a.GmailAttachmentId)
                            .ToListAsync(HttpContext.RequestAborted);
                        var connues = dejaEnBase.ToHashSet(StringComparer.Ordinal);

                        var creates = 0;

                        foreach (var part in apiParts)
                        {
                            if (!connues.Add(part.GmailAttachmentId)) continue;

                            // La ligne EST la seule chose qui manquait : le contenu n'a
                            // jamais été téléchargé, il est relu à la demande depuis
                            // Gmail quand l'utilisateur ouvre la pièce.
                            _context.GmailAttachments.Add(new GmailAttachment
                            {
                                GmailMessageId = candidat.Id,
                                GmailAttachmentId = part.GmailAttachmentId,
                                FileName = part.FileName,
                                MimeType = part.MimeType,
                                SizeBytes = part.SizeBytes,
                                IsInline = part.IsInline,
                                ContentId = part.ContentId,
                                CreatedAt = DateTime.UtcNow
                            });
                            creates++;
                        }

                        if (creates > 0)
                        {
                            await _context.SaveChangesAsync(HttpContext.RequestAborted);
                            creees += creates;
                            details.Add($"Message {candidat.Id} : {creates} pièce(s) créée(s).");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Un message en erreur (supprimé côté Gmail, quota, réseau) ne doit
                        // pas interrompre le rattrapage : on le compte et on continue.
                        erreurs++;
                        _logger.LogWarning(
                            ex, "Rattrapage A2b : message {MessageId} ignoré", candidat.Id);
                        details.Add($"Message {candidat.Id} : ignoré ({ex.GetType().Name}).");
                    }
                }
            }

            return Ok(BuildBackfillReport(traites, creees, erreurs, restants, details));
        }

        private static AttachmentBackfillDto BuildBackfillReport(
            int traites, int creees, int erreurs, int restants, List<string> details) =>
            new()
            {
                Examines = traites,
                Creees = creees,
                Erreurs = erreurs,
                Restants = restants,
                Messages = details
            };

        /// <summary>
        /// Garde commun des maintenances Gmail : administrateur de l'application, sinon 403.
        /// <para>
        /// Le rôle applicatif ne suffit pas — ces endpoints relancent une boîte Gmail et
        /// consomment le quota du compte qui la porte, ce qui n'a rien à voir avec le
        /// droit d'écrire dans le module « courriels ».
        /// </para>
        /// </summary>
        private async Task<IActionResult?> GardeMaintenanceAttachmentsAsync(string operation)
        {
            var userId = _userManager.GetUserId(User);
            var demandeur = userId != null
                ? await _userManager.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId)
                : null;

            if (demandeur?.Role?.EstAdministrateur != true)
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = $"Réservé aux administrateurs : {operation} consomme le quota Gmail du compte concerné."
                });

            return null;
        }

        /// <summary>Valide et normalise une liste d'adresses : une adresse invalide bloque tout l'envoi.</summary>
        private static List<string> NormalizeAddresses(List<string>? addresses)
        {
            var result = new List<string>();
            if (addresses == null) return result;

            foreach (var raw in addresses)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                foreach (var candidate in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = candidate.Trim();
                    if (trimmed.Length == 0 || trimmed.Length > 320) continue;

                    // Validation volontairement simple mais réelle : un point-virgule dans
                    // une adresse injecterait un second destinataire non choisi par l'utilisateur.
                    if (!System.Net.Mail.MailAddress.TryCreate(trimmed, out var parsed)) continue;
                    if (!string.Equals(parsed!.Address, trimmed, StringComparison.OrdinalIgnoreCase)) continue;

                    result.Add(trimmed);
                }
            }

            return result;
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
