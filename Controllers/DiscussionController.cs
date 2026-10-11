using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Discussion;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module Discussion (type Slack) — étape E1 : backend sans temps réel.
    ///
    /// Règles invariantes :
    /// <list type="bullet">
    /// <item>Toutes les routes exigent le drapeau <c>PeutUtiliserDiscussion</c>
    /// (attribut de classe), un compte actif (vérifié par le middleware de session)
    /// et un compte INTERNE (attribut de classe <see cref="RequireCompteInterneAttribute"/>).</item>
    /// <item>Toute opération portant sur une conversation vérifie l'APPARTENANCE :
    /// un non-membre reçoit 404, JAMAIS 403 — répondre 403 confirmerait l'existence de
    /// la conversation, et donc des fuites d'information entre utilisateurs.</item>
    /// <item>Le contenu est du texte BRUT, jamais interprété en HTML, borné à 4000
    /// caractères par message et à 30 messages/minute par auteur.</item>
    /// <item>Aucune de ces routes n'est atteignable par un lien de partage : elles
    /// exigent une session authentifiée, et le module Partage n'a aucune route ici.</item>
    /// </list>
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [RequireModulePermission("discussion")]
    [RequireCompteInterne]
    public class DiscussionController : ControllerBase
    {
        /// <summary>Taille d'une page d'historique.</summary>
        private const int TaillePage = 30;

        /// <summary>Plafond d'envoi par utilisateur, par minute.</summary>
        private const int MaxMessagesParMinute = 30;

        /// <summary>Longueur de l'aperçu du dernier message dans la liste des conversations.</summary>
        private const int LongueurApercu = 120;

        /// <summary>Extraction des mentions : @ suivi de lettres, chiffres, . _ -</summary>
        private static readonly Regex MotifMention =
            new(@"@([\p{L}\p{N}._-]+)", RegexOptions.Compiled);

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IPermissionService _permissions;

        public DiscussionController(
            ApplicationDbContext db,
            INotificationService notifications,
            IPermissionService permissions)
        {
            _db = db;
            _notifications = notifications;
            _permissions = permissions;
        }

        private string? Moi => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // ─────────────────────────────────────────────────────────────────────────
        // GET api/Discussion — mes conversations
        // ─────────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> MesConversations()
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            var ct = HttpContext.RequestAborted;

            // Tout est calculé EN SQL : les non-lus sont un Count côté base, jamais un
            // chargement complet des messages côté application.
            var lignes = await _db.ConversationMembres
                .AsNoTracking()
                .Where(m => m.UserId == moi && m.AQuitteLe == null)
                .Select(m => new
                {
                    m.ConversationId,
                    m.EstAdmin,
                    m.DernierMessageLuId,
                    Conversation = m.Conversation,
                    Membres = m.Conversation.Membres
                        .Where(x => x.AQuitteLe == null)
                        .Select(x => new MembreDto
                        {
                            UserId = x.UserId,
                            EstAdmin = x.EstAdmin,
                            Muet = x.Muet,
                            RejointLe = x.RejointLe,
                            AQuitteLe = x.AQuitteLe,
                            NomAffichage = x.User.UserName ?? x.User.Nom
                        })
                        .ToList(),
                    Dernier = m.Conversation.Messages
                        .Where(x => x.SupprimeLe == null)
                        .OrderByDescending(x => x.Id)
                        .Select(x => new { x.EnvoyeLe, x.Contenu })
                        .FirstOrDefault(),
                    NonLus = m.Conversation.Messages.Count(x =>
                        x.Id > m.DernierMessageLuId && x.AuteurUserId != moi && x.SupprimeLe == null)
                })
                .ToListAsync(ct);

            var result = lignes.Select(l => new ConversationDto
            {
                Id = l.ConversationId,
                Type = l.Conversation.Type.ToString(),
                Nom = l.Conversation.Nom,
                EstPrive = l.Conversation.EstPrive,
                EstAdmin = l.EstAdmin,
                CreeLe = l.Conversation.CreeLe,
                ArchiveLe = l.Conversation.ArchiveLe,
                NonLus = l.NonLus,
                DernierMessageLe = l.Dernier?.EnvoyeLe,
                DernierMessageApercu = l.Dernier == null ? null : Apercu(l.Dernier.Contenu),
                Membres = l.Membres
            })
            // Tri par activité : dernier message d'abord, conversations sans message ensuite.
            .OrderByDescending(c => c.DernierMessageLe ?? DateTime.MinValue)
            .ThenByDescending(c => c.Id)
            .ToList();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST api/Discussion — créer un DM, un groupe ou un canal
        // ─────────────────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Creer([FromBody] CreerConversationDto dto)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            if (!Enum.TryParse<TypeConversation>(dto.Type, ignoreCase: true, out var type))
                return BadRequest(new { message = "Type invalide : attendu Direct, Groupe ou Canal." });

            // Seuls les comptes actifs et internes participent à la Discussion.
            var destinataires = dto.MembreIds
                .Where(id => !string.IsNullOrWhiteSpace(id) && id != moi)
                .Distinct()
                .ToList();

            var valides = await _db.Users
                .Where(u => destinataires.Contains(u.Id) && u.EstActif && u.EstCompteInterne)
                .Select(u => u.Id)
                .ToListAsync();

            if (valides.Count != destinataires.Count)
                return BadRequest(new { message = "Un ou plusieurs destinataires sont inconnus ou inactifs." });

            if (type == TypeConversation.Direct)
                return await CreerDirectAsync(dto, moi, valides);

            return await CreerGroupeOuCanalAsync(dto, moi, valides, type);
        }

        private async Task<IActionResult> CreerDirectAsync(
            CreerConversationDto dto, string moi, List<string> valides)
        {
            if (valides.Count != 1)
                return BadRequest(new { message = "Un message direct se crée avec exactement un destinataire." });

            var autre = valides[0];

            // Clé de déduplication indépendante de l'ordre : "idMin:idMax". L'index
            // unique sur CleDirecte interdit le doublon même sous requêtes simultanées.
            var cle = string.CompareOrdinal(moi, autre) < 0 ? $"{moi}:{autre}" : $"{autre}:{moi}";

            var existante = await _db.Conversations
                .FirstOrDefaultAsync(c => c.CleDirecte == cle);

            if (existante != null)
                return Ok(await ProjectionConversationAsync(existante.Id, moi));

            var conversation = new Conversation
            {
                Type = TypeConversation.Direct,
                EstPrive = true,
                CleDirecte = cle,
                CreeParUserId = moi,
                CreeLe = DateTime.Now
            };
            conversation.Membres.Add(new ConversationMembre { UserId = moi, EstAdmin = true, RejointLe = DateTime.Now });
            conversation.Membres.Add(new ConversationMembre { UserId = autre, EstAdmin = true, RejointLe = DateTime.Now });

            _db.Conversations.Add(conversation);
            try
            {
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
            }
            catch (DbUpdateException)
            {
                // Course perdue sur la contrainte unique : la conversation existe
                // désormais, on la renvoie au lieu d'échouer.
                _db.ChangeTracker.Clear();
                var gagnante = await _db.Conversations.FirstOrDefaultAsync(c => c.CleDirecte == cle);
                if (gagnante != null)
                    return Ok(await ProjectionConversationAsync(gagnante.Id, moi));
                throw;
            }

            return StatusCode(StatusCodes.Status201Created,
                await ProjectionConversationAsync(conversation.Id, moi));
        }

        private async Task<IActionResult> CreerGroupeOuCanalAsync(
            CreerConversationDto dto, string moi, List<string> valides, TypeConversation type)
        {
            if (string.IsNullOrWhiteSpace(dto.Nom))
                return BadRequest(new { message = "Un groupe ou un canal doit avoir un nom." });

            if (type == TypeConversation.Groupe && !dto.EstPrive)
                return BadRequest(new { message = "Un groupe est toujours privé." });

            // Un canal PUBLIC n'est pas une affaire privée : sa création relève de la
            // modération, donc de PeutGererDiscussion. Un groupe ou un canal privé se
            // crée avec le seul droit d'utiliser le module.
            if (type == TypeConversation.Canal && !dto.EstPrive
                && !await _permissions.CanGererDiscussionAsync(moi))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "La création d'un canal public nécessite le droit de gérer la discussion."
                });
            }

            var conversation = new Conversation
            {
                Type = type,
                Nom = dto.Nom.Trim(),
                EstPrive = type == TypeConversation.Groupe || dto.EstPrive,
                CreeParUserId = moi,
                CreeLe = DateTime.Now
            };
            conversation.Membres.Add(new ConversationMembre { UserId = moi, EstAdmin = true, RejointLe = DateTime.Now });
            foreach (var id in valides)
                conversation.Membres.Add(new ConversationMembre { UserId = id, EstAdmin = false, RejointLe = DateTime.Now });

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            return StatusCode(StatusCodes.Status201Created,
                await ProjectionConversationAsync(conversation.Id, moi));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET api/Discussion/utilisateurs — annuaire (actifs et internes, sans moi)
        // ─────────────────────────────────────────────────────────────────────────

        [HttpGet("utilisateurs")]
        public async Task<IActionResult> Utilisateurs()
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            // Un compte désactivé OU externe n'apparaît jamais dans l'annuaire.
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id != moi && u.EstActif && u.EstCompteInterne)
                .OrderBy(u => u.Nom).ThenBy(u => u.Prenom)
                .Select(u => new UtilisateurDiscussionDto
                {
                    UserId = u.Id,
                    Nom = u.Nom,
                    Prenom = u.Prenom ?? string.Empty,
                    Poste = u.Poste,
                    NomAffichage = string.IsNullOrWhiteSpace(u.Prenom) ? u.Nom : u.Prenom + " " + u.Nom
                })
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(users);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET api/Discussion/{id}/messages?avantId= — historique paginé (30)
        // ─────────────────────────────────────────────────────────────────────────

        [HttpGet("{id:int}/messages")]
        public async Task<IActionResult> Messages(int id, [FromQuery] long? avantId)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            if (!await EstMembreAsync(id, moi))
                return NotFound();

            var ct = HttpContext.RequestAborted;

            var requete = _db.MessagesDiscussion
                .AsNoTracking()
                .Where(m => m.ConversationId == id);

            if (avantId is long curseur)
                requete = requete.Where(m => m.Id < curseur);

            // On prend les 30 plus récents sous le curseur, puis on remet en
            // ordre CHRONOLOGIQUE croissant pour l'affichage.
            var page = await requete
                .OrderByDescending(m => m.Id)
                .Take(TaillePage)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    AuteurUserId = m.AuteurUserId,
                    // Nom de l'auteur résolu par sous-requête : AuteurUserId n'a pas de
                    // navigation (pas de FK vers AspNetUsers), voir DiscussionModels.cs.
                    AuteurNom = _db.Users
                        .Where(u => u.Id == m.AuteurUserId)
                        .Select(u => string.IsNullOrWhiteSpace(u.Prenom) ? u.Nom : u.Prenom + " " + u.Nom)
                        .FirstOrDefault() ?? m.AuteurUserId,
                    Contenu = m.SupprimeLe == null ? m.Contenu : string.Empty,
                    EnvoyeLe = m.EnvoyeLe,
                    ModifieLe = m.ModifieLe,
                    EstSupprime = m.SupprimeLe != null,
                    ReponseAId = m.ReponseAId,
                    Revision = m.Revision
                })
                .ToListAsync(ct);

            page.Reverse();
            return Ok(page);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST api/Discussion/{id}/messages — envoyer (rate limit 30/min)
        // ─────────────────────────────────────────────────────────────────────────

        [HttpPost("{id:int}/messages")]
        public async Task<IActionResult> Envoyer(int id, [FromBody] EnvoyerMessageDto dto)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            if (!await EstMembreAsync(id, moi))
                return NotFound();

            var contenu = dto.Contenu?.Trim() ?? string.Empty;
            if (contenu.Length == 0)
                return BadRequest(new { message = "Le message ne peut pas être vide." });
            if (contenu.Length > 4000)
                return BadRequest(new { message = "Le message dépasse 4000 caractères." });

            // Rate limit par AUTEUR, calculé en SQL sur la fenêtre glissante d'une minute.
            var depuis = DateTime.Now.AddMinutes(-1);
            var recents = await _db.MessagesDiscussion.CountAsync(
                m => m.AuteurUserId == moi && m.EnvoyeLe >= depuis,
                HttpContext.RequestAborted);
            if (recents >= MaxMessagesParMinute)
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = $"Trop de messages envoyés. Réessayez dans une minute (limite : {MaxMessagesParMinute}/min)."
                });

            if (dto.ReponseAId is long reponseA
                && !await _db.MessagesDiscussion.AnyAsync(m => m.Id == reponseA && m.ConversationId == id))
            {
                return BadRequest(new { message = "Le message auquel vous répondez n'existe pas dans cette conversation." });
            }

            var message = new MessageDiscussion
            {
                ConversationId = id,
                AuteurUserId = moi,
                Contenu = contenu,
                EnvoyeLe = DateTime.Now,
                ReponseAId = dto.ReponseAId
            };

            _db.MessagesDiscussion.Add(message);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            // La revision a été attribuée par la base (default/trigger) et relue par EF.
            var projection = await ProjectionMessageAsync(message.Id, moi);

            await NotifierMentionsAsync(id, message, contenu, moi);

            return StatusCode(StatusCodes.Status201Created, projection);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PUT api/Discussion/{id}/messages/{msgId} — modifier SON message
        // ─────────────────────────────────────────────────────────────────────────

        [HttpPut("{id:int}/messages/{msgId:long}")]
        public async Task<IActionResult> Modifier(int id, long msgId, [FromBody] ModifierMessageDto dto)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            if (!await EstMembreAsync(id, moi))
                return NotFound();

            var message = await _db.MessagesDiscussion
                .FirstOrDefaultAsync(m => m.Id == msgId && m.ConversationId == id);
            if (message == null) return NotFound();

            // Modifier le message d'un AUTRE membre : interdit. Membre identifié,
            // donc 403 (pas 404 : la conversation et le message lui sont visibles).
            if (message.AuteurUserId != moi)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Vous ne pouvez modifier que vos propres messages." });

            if (message.SupprimeLe != null)
                return BadRequest(new { message = "Ce message a été supprimé." });

            var contenu = dto.Contenu?.Trim() ?? string.Empty;
            if (contenu.Length == 0)
                return BadRequest(new { message = "Le message ne peut pas être vide." });
            if (contenu.Length > 4000)
                return BadRequest(new { message = "Le message dépasse 4000 caractères." });

            message.Contenu = contenu;
            message.ModifieLe = DateTime.Now;

            // Le trigger PostgreSQL renouvelle la revision à chaque UPDATE : les
            // clients en polling verront le message modifié.
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            return Ok(await ProjectionMessageAsync(msgId, moi));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // DELETE api/Discussion/{id}/messages/{msgId} — supprimer SON message
        // ─────────────────────────────────────────────────────────────────────────

        [HttpDelete("{id:int}/messages/{msgId:long}")]
        public async Task<IActionResult> Supprimer(int id, long msgId)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            if (!await EstMembreAsync(id, moi))
                return NotFound();

            var message = await _db.MessagesDiscussion
                .FirstOrDefaultAsync(m => m.Id == msgId && m.ConversationId == id);
            if (message == null) return NotFound();

            if (message.AuteurUserId != moi)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Vous ne pouvez supprimer que vos propres messages." });

            if (message.SupprimeLe != null)
                return NoContent();

            // Suppression LOGIQUE : le contenu est vidé (plus rien à lire) mais la
            // ligne reste, avec une nouvelle revision pour que /sync la propage.
            message.Contenu = string.Empty;
            message.SupprimeLe = DateTime.Now;

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return NoContent();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST api/Discussion/{id}/lire — marquer lu jusqu'à un message
        // ─────────────────────────────────────────────────────────────────────────

        [HttpPost("{id:int}/lire")]
        public async Task<IActionResult> Lire(int id, [FromBody] MarquerLuDto dto)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            var membre = await _db.ConversationMembres
                .FirstOrDefaultAsync(m => m.ConversationId == id && m.UserId == moi && m.AQuitteLe == null);
            if (membre == null) return NotFound();

            // Le pointeur ne recule jamais : relire un ancien message ne « dé-lit » pas.
            if (dto.JusquaMessageId > membre.DernierMessageLuId)
            {
                membre.DernierMessageLuId = dto.JusquaMessageId;
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
            }

            return NoContent();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST api/Discussion/{id}/membres — ajouter (admin de groupe/canal)
        // ─────────────────────────────────────────────────────────────────────────

        [HttpPost("{id:int}/membres")]
        public async Task<IActionResult> AjouterMembres(int id, [FromBody] AjouterMembresDto dto)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            var conversation = await _db.Conversations
                .Include(c => c.Membres)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) return NotFound();

            var membre = conversation.Membres.FirstOrDefault(m => m.UserId == moi && m.AQuitteLe == null);
            if (membre == null) return NotFound();

            // Un DM a un ensemble de membres figé : on n'y ajoute personne.
            if (conversation.Type == TypeConversation.Direct)
                return BadRequest(new { message = "On ne peut pas ajouter de membre à un message direct." });

            // Droit d'ajouter : admin de la conversation, OU PeutGererDiscussion sur
            // un CANAL PUBLIC (modération). Jamais sur un groupe ou un canal privé.
            var peutAjouter = membre.EstAdmin
                || (conversation.Type == TypeConversation.Canal
                    && !conversation.EstPrive
                    && await _permissions.CanGererDiscussionAsync(moi));

            if (!peutAjouter)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Seul un administrateur de la conversation peut ajouter des membres." });

            var demandes = dto.MembreIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var valides = await _db.Users
                .Where(u => demandes.Contains(u.Id) && u.EstActif && u.EstCompteInterne)
                .Select(u => u.Id)
                .ToListAsync(HttpContext.RequestAborted);

            if (valides.Count != demandes.Count)
                return BadRequest(new { message = "Un ou plusieurs utilisateurs sont inconnus ou inactifs." });

            foreach (var userId in valides)
            {
                var existant = conversation.Membres.FirstOrDefault(m => m.UserId == userId);
                if (existant == null)
                {
                    conversation.Membres.Add(new ConversationMembre
                    {
                        ConversationId = id,
                        UserId = userId,
                        EstAdmin = false,
                        RejointLe = DateTime.Now
                    });
                }
                else if (existant.AQuitteLe != null)
                {
                    // Réintégration : la ligne existante est réactivée (index unique).
                    existant.AQuitteLe = null;
                    existant.RejointLe = DateTime.Now;
                }
            }

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(await ProjectionConversationAsync(id, moi));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // DELETE api/Discussion/{id}/quitter — quitter un groupe / canal
        // ─────────────────────────────────────────────────────────────────────────

        [HttpDelete("{id:int}/quitter")]
        public async Task<IActionResult> Quitter(int id)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null) return NotFound();

            var membre = await _db.ConversationMembres
                .FirstOrDefaultAsync(m => m.ConversationId == id && m.UserId == moi && m.AQuitteLe == null);
            if (membre == null) return NotFound();

            // Quitter un DM n'a pas de sens : il n'y a que deux participants, et un
            // départ transformerait la conversation en fil orphelin.
            if (conversation.Type == TypeConversation.Direct)
                return BadRequest(new { message = "On ne quitte pas un message direct." });

            membre.AQuitteLe = DateTime.Now;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return NoContent();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET api/Discussion/sync?depuis=<curseur> — alimente le polling (E2)
        // ─────────────────────────────────────────────────────────────────────────

        [HttpGet("sync")]
        public async Task<IActionResult> Sync([FromQuery] long depuis)
        {
            var moi = Moi;
            if (moi == null) return Unauthorized();

            var ct = HttpContext.RequestAborted;

            var mesIds = await _db.ConversationMembres
                .AsNoTracking()
                .Where(m => m.UserId == moi && m.AQuitteLe == null)
                .Select(m => m.ConversationId)
                .ToListAsync(ct);

            // Messages créés, modifiés OU supprimés (une modification/suppression
            // renouvelle la revision par trigger) dans MES conversations.
            var messages = await _db.MessagesDiscussion
                .AsNoTracking()
                .Where(m => mesIds.Contains(m.ConversationId) && m.Revision > depuis)
                .OrderBy(m => m.Revision)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    AuteurUserId = m.AuteurUserId,
                    // Nom de l'auteur résolu par sous-requête : AuteurUserId n'a pas de
                    // navigation (pas de FK vers AspNetUsers), voir DiscussionModels.cs.
                    AuteurNom = _db.Users
                        .Where(u => u.Id == m.AuteurUserId)
                        .Select(u => string.IsNullOrWhiteSpace(u.Prenom) ? u.Nom : u.Prenom + " " + u.Nom)
                        .FirstOrDefault() ?? m.AuteurUserId,
                    Contenu = m.SupprimeLe == null ? m.Contenu : string.Empty,
                    EnvoyeLe = m.EnvoyeLe,
                    ModifieLe = m.ModifieLe,
                    EstSupprime = m.SupprimeLe != null,
                    ReponseAId = m.ReponseAId,
                    Revision = m.Revision
                })
                .ToListAsync(ct);

            // Snapshot complet de MES conversations et de leurs membres : les
            // changements d'appartenance (ajout, départ) sont ainsi reflétés même
            // sans nouveau message. Le volume reste faible (quelques dizaines).
            var conversations = await ProjeterMesConversationsAsync(moi, ct);

            var curseur = messages.Count > 0 ? messages[^1].Revision : depuis;

            return Ok(new SyncDto
            {
                Curseur = curseur,
                Messages = messages,
                Conversations = conversations
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Vrai si l'utilisateur est un membre ACTIF de la conversation.</summary>
        private Task<bool> EstMembreAsync(int conversationId, string userId) =>
            _db.ConversationMembres.AnyAsync(m =>
                m.ConversationId == conversationId && m.UserId == userId && m.AQuitteLe == null);

        private async Task<List<ConversationDto>> ProjeterMesConversationsAsync(string moi, CancellationToken ct)
        {
            var lignes = await _db.ConversationMembres
                .AsNoTracking()
                .Where(m => m.UserId == moi && m.AQuitteLe == null)
                .Select(m => new
                {
                    m.ConversationId,
                    m.EstAdmin,
                    m.DernierMessageLuId,
                    Conversation = m.Conversation,
                    Membres = m.Conversation.Membres
                        .Where(x => x.AQuitteLe == null)
                        .Select(x => new MembreDto
                        {
                            UserId = x.UserId,
                            EstAdmin = x.EstAdmin,
                            Muet = x.Muet,
                            RejointLe = x.RejointLe,
                            AQuitteLe = x.AQuitteLe,
                            NomAffichage = x.User.UserName ?? x.User.Nom
                        })
                        .ToList(),
                    Dernier = m.Conversation.Messages
                        .Where(x => x.SupprimeLe == null)
                        .OrderByDescending(x => x.Id)
                        .Select(x => new { x.EnvoyeLe, x.Contenu })
                        .FirstOrDefault(),
                    NonLus = m.Conversation.Messages.Count(x =>
                        x.Id > m.DernierMessageLuId && x.AuteurUserId != moi && x.SupprimeLe == null)
                })
                .ToListAsync(ct);

            return lignes.Select(l => new ConversationDto
            {
                Id = l.ConversationId,
                Type = l.Conversation.Type.ToString(),
                Nom = l.Conversation.Nom,
                EstPrive = l.Conversation.EstPrive,
                EstAdmin = l.EstAdmin,
                CreeLe = l.Conversation.CreeLe,
                ArchiveLe = l.Conversation.ArchiveLe,
                NonLus = l.NonLus,
                DernierMessageLe = l.Dernier?.EnvoyeLe,
                DernierMessageApercu = l.Dernier == null ? null : Apercu(l.Dernier.Contenu),
                Membres = l.Membres
            }).ToList();
        }

        private async Task<ConversationDto> ProjectionConversationAsync(int id, string moi)
        {
            var touts = await ProjeterMesConversationsAsync(moi, HttpContext.RequestAborted);
            return touts.First(c => c.Id == id);
        }

        private async Task<MessageDto> ProjectionMessageAsync(long msgId, string moi)
        {
            return await _db.MessagesDiscussion
                .AsNoTracking()
                .Where(m => m.Id == msgId)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    AuteurUserId = m.AuteurUserId,
                    // Nom de l'auteur résolu par sous-requête : AuteurUserId n'a pas de
                    // navigation (pas de FK vers AspNetUsers), voir DiscussionModels.cs.
                    AuteurNom = _db.Users
                        .Where(u => u.Id == m.AuteurUserId)
                        .Select(u => string.IsNullOrWhiteSpace(u.Prenom) ? u.Nom : u.Prenom + " " + u.Nom)
                        .FirstOrDefault() ?? m.AuteurUserId,
                    Contenu = m.SupprimeLe == null ? m.Contenu : string.Empty,
                    EnvoyeLe = m.EnvoyeLe,
                    ModifieLe = m.ModifieLe,
                    EstSupprime = m.SupprimeLe != null,
                    ReponseAId = m.ReponseAId,
                    Revision = m.Revision
                })
                .FirstAsync(HttpContext.RequestAborted);
        }

        /// <summary>
        /// Détecte les mentions @ dans le message et notifie les membres concernés.
        /// La correspondance se fait sur UserName ou Nom, insensible à la casse, et
        /// UNIQUEMENT parmi les membres actifs de la conversation : on ne peut pas
        /// notifier quelqu'un qui n'a pas accès au message. Muet et l'auteur sont exclus.
        /// </summary>
        private async Task NotifierMentionsAsync(int conversationId, MessageDiscussion message, string contenu, string moi)
        {
            var tokens = MotifMention.Matches(contenu)
                .Select(x => x.Groups[1].Value.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (tokens.Count == 0) return;

            var membres = await _db.ConversationMembres
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId && m.AQuitteLe == null && m.UserId != moi && !m.Muet)
                .Select(m => new
                {
                    m.UserId,
                    m.User.UserName,
                    m.User.Nom
                })
                .ToListAsync(HttpContext.RequestAborted);

            var conversation = await _db.Conversations
                .AsNoTracking()
                .Where(c => c.Id == conversationId)
                .Select(c => new { c.Type, c.Nom })
                .FirstAsync(HttpContext.RequestAborted);

            var nomConversation = string.IsNullOrWhiteSpace(conversation.Nom)
                ? "une conversation privée"
                : conversation.Nom!;

            var deja = new HashSet<string>();

            foreach (var membre in membres)
            {
                var motifs = new[] { membre.UserName, membre.Nom }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.ToLowerInvariant());

                if (!motifs.Any(motif => tokens.Contains(motif)))
                    continue;

                if (!deja.Add(membre.UserId))
                    continue;

                await _notifications.NotifierMentionAsync(
                    membre.UserId,
                    moi,
                    conversationId,
                    nomConversation,
                    message.Id,
                    HttpContext.RequestAborted);
            }
        }

        private static string? Apercu(string? contenu)
        {
            if (string.IsNullOrEmpty(contenu)) return null;
            var uneLigne = contenu.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return uneLigne.Length <= LongueurApercu ? uneLigne : uneLigne[..LongueurApercu] + "…";
        }
    }
}
