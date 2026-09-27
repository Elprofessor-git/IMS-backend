using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Dtos;
using Backend_Gestion_Magasin_API.Services;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module Tâches (production).
    ///
    /// Deux niveaux d'autorisation distincts et cumulés :
    ///   1. permission de MODULE  — [RequireModulePermission("taches", …)] via RequireModulePermissionAttribute ;
    ///   2. propriété de la RESSOURCE — vérifiée ici, via ITacheOwnershipService.
    ///
    /// Convention de sécurité : une tâche qui n'appartient pas à l'utilisateur et à
    /// laquelle il n'a pas de droit global renvoie 404, jamais 403, afin de ne pas
    /// divulguer l'existence d'une ressource appartenant à autrui. C'est la convention
    /// déjà appliquée par le module Courriels. Le 403 reste réservé aux refus de portée
    /// globale (scope=all sans droit).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TacheProductionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ITacheOwnershipService _ownership;
        private readonly IPermissionService _permissions;
        private readonly INotificationService _notifications;

        public TacheProductionController(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            ITacheOwnershipService ownership,
            IPermissionService permissions,
            INotificationService notifications)
        {
            _context = context;
            _currentUser = currentUser;
            _ownership = ownership;
            _permissions = permissions;
            _notifications = notifications;
        }

        // ══════════════════════════ Lecture ══════════════════════════

        /// <summary>
        /// GET api/TacheProduction?scope=mine|all
        /// « mine » (défaut) ne dépend QUE de l'utilisateur authentifié.
        /// « all » exige PeutVoirToutesTaches, sinon 403.
        /// </summary>
        [HttpGet]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<TacheReadDto>>> GetTaches(
            [FromQuery] TacheScope scope = TacheScope.Mine)
        {
            var userId = _currentUser.UserId!;
            var canAccessAll = await _ownership.CanAccessAllAsync(userId);

            if (scope == TacheScope.All && !canAccessAll)
                return Forbid();

            var query = _ownership.ApplyVisibility(
                _context.TachesProduction
                    .Include(t => t.CommandeClient).ThenInclude(c => c!.Client)
                    .AsQueryable(),
                userId,
                canAccessAll);

            var taches = await query.ToListAsync();
            return Ok(await MapReadAsync(taches));
        }

        [HttpGet("{id}")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<TacheReadDto>> GetTacheProduction(int id)
        {
            var userId = _currentUser.UserId!;
            var canAccessAll = await _ownership.CanAccessAllAsync(userId);

            // Le filtre d'ownership est appliqué DANS la requête : impossible d'oublier
            // la condition, et une tâche d'autrui renvoie 404 comme une tâche inexistante.
            var tache = await _ownership.ApplyVisibility(
                _context.TachesProduction
                    .Include(t => t.CommandeClient).ThenInclude(c => c!.Client)
                    .Include(t => t.MouvementsStock).ThenInclude(m => m.Stock).ThenInclude(s => s.Article)
                    .AsNoTracking(),
                userId,
                canAccessAll)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tache == null)
                return NotFound();

            return Ok(await MapReadAsync(tache));
        }

        [HttpGet("Statut/{statut}")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<TacheReadDto>>> GetTachesByStatut(StatutTache statut)
        {
            var userId = _currentUser.UserId!;
            var canAccessAll = await _ownership.CanAccessAllAsync(userId);

            var query = _ownership.ApplyVisibility(
                _context.TachesProduction
                    .Include(t => t.CommandeClient).ThenInclude(c => c!.Client)
                    .Where(t => t.Statut == statut),
                userId,
                canAccessAll);

            return Ok(await MapReadAsync(await query.ToListAsync()));
        }

        /// <summary>
        /// Filtre par équipe. Soumis à la MÊME filtre de visibilité que la collection :
        /// cette route ne doit pas devenir un moyen de contourner l'ownership.
        /// </summary>
        [HttpGet("Equipe/{equipe}")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<TacheReadDto>>> GetTachesByEquipe(string equipe)
        {
            var userId = _currentUser.UserId!;
            var canAccessAll = await _ownership.CanAccessAllAsync(userId);

            var query = _ownership.ApplyVisibility(
                _context.TachesProduction
                    .Include(t => t.CommandeClient).ThenInclude(c => c!.Client)
                    .Where(t => t.EquipeAssignee == equipe),
                userId,
                canAccessAll);

            return Ok(await MapReadAsync(await query.ToListAsync()));
        }

        [HttpGet("Dashboard")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<object>> GetDashboard()
        {
            var userId = _currentUser.UserId!;
            var canAccessAll = await _ownership.CanAccessAllAsync(userId);

            var baseQuery = _ownership.ApplyVisibility(
                _context.TachesProduction.AsQueryable(), userId, canAccessAll);

            var dashboard = new
            {
                TotalTaches = await baseQuery.CountAsync(),
                NonCommencees = await baseQuery.CountAsync(t => t.Statut == StatutTache.NonCommence),
                EnCours = await baseQuery.CountAsync(t => t.Statut == StatutTache.EnCours),
                Bloquees = await baseQuery.CountAsync(t => t.Statut == StatutTache.Bloque),
                Terminees = await baseQuery.CountAsync(t => t.Statut == StatutTache.Termine),
                TachesUrgentes = await baseQuery.CountAsync(t => t.Priorite == PrioriteTache.Urgente && t.Statut != StatutTache.Termine),
                TachesEnRetard = await baseQuery.CountAsync(t => t.DateFinPrevue < DateTime.Now && t.Statut != StatutTache.Termine),
                AvancementMoyen = await baseQuery
                    .Where(t => t.Statut != StatutTache.Termine && t.Statut != StatutTache.Annule)
                    .AverageAsync(t => (double?)t.PourcentageAvancement) ?? 0,
                // Indique au client s'il peut proposer le mode « Toutes les tâches ».
                PeutVoirToutesLesTaches = canAccessAll
            };

            return Ok(dashboard);
        }

        /// <summary>
        /// Utilisateurs proposables à l'assignation. Le client ne saisit jamais un Id :
        /// il choisit parmi cette liste, et le serveur revalide.
        /// </summary>
        [HttpGet("UtilisateursAssignables")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<UtilisateurAssignableDto>>> GetUtilisateursAssignables()
        {
            var utilisateurs = await _context.Users
                .Where(u => u.EstActif)
                .OrderBy(u => u.Nom).ThenBy(u => u.Prenom)
                .Select(u => new UtilisateurAssignableDto
                {
                    Id = u.Id,
                    DisplayName = string.IsNullOrWhiteSpace(u.Prenom) ? u.Nom : u.Prenom + " " + u.Nom
                })
                .ToListAsync();

            return Ok(utilisateurs);
        }

        // ══════════════════════════ Écriture ══════════════════════════

        /// <summary>
        /// POST api/TacheProduction — le propriétaire est DÉCIDÉ PAR LE SERVEUR.
        /// CreatedByUserId vaut toujours l'utilisateur courant ; le client ne peut ni
        /// le fournir ni le modifier.
        /// </summary>
        [HttpPost]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult<TacheReadDto>> PostTacheProduction(
            [FromBody] CreateTacheProductionDto dto)
        {
            var userId = _currentUser.UserId!;

            if (string.IsNullOrWhiteSpace(dto.Titre))
                return BadRequest(new { message = "Le titre de la tâche est requis." });

            var priorite = PrioriteTache.Normale;
            if (!string.IsNullOrWhiteSpace(dto.Priorite)
                && !Enum.TryParse<PrioriteTache>(dto.Priorite, true, out priorite))
                return BadRequest(new { message = "Priorité invalide." });

            if (!string.IsNullOrWhiteSpace(dto.Statut)
                && !Enum.TryParse<StatutTache>(dto.Statut, true, out _))
                return BadRequest(new { message = "Statut invalide." });

            var tache = new TacheProduction
            {
                Titre = dto.Titre.Trim(),
                Description = dto.Description,
                EquipeAssignee = dto.EquipeAssignee,
                Priorite = priorite,
                Statut = string.IsNullOrWhiteSpace(dto.Statut)
                    ? StatutTache.NonCommence
                    : Enum.Parse<StatutTache>(dto.Statut, true),
                DateDebutPrevue = dto.DateDebutPrevue,
                DateFinPrevue = dto.DateFinPrevue,
                DureeEstimeeHeures = dto.DureeEstimeeHeures,
                NotesProgression = dto.NotesProgression,
                DateCreation = DateTime.Now,

                // ── Identité décidée côté serveur, jamais depuis le corps de la requête.
                CreatedByUserId = userId,
                CreePar = _currentUser.UserName
            };

            if (dto.CommandeClientId is int commandeId)
            {
                if (!await _context.CommandesClients.AnyAsync(c => c.Id == commandeId))
                    return BadRequest(new { message = "Commande introuvable." });
                tache.CommandeClientId = commandeId;
            }

            if (dto.GroupeTacheId is int groupeId)
            {
                if (!await _context.GroupesTaches.AnyAsync(g => g.Id == groupeId))
                    return BadRequest(new { message = "Groupe de tâches introuvable." });
                tache.GroupeTacheId = groupeId;
            }

            var erreurAssignation = await ApplyAssignationAsync(
                tache, dto.AssignedToUserId, userId, desassignerSiVide: false);
            if (erreurAssignation != null)
                return erreurAssignation;

            // Responsable attendu AVANT enregistrement : une tâche neuve n'a pas d'ancien
            // responsable, donc personne n'est « désassigné ».
            _context.TachesProduction.Add(tache);
            await _context.SaveChangesAsync();

            // Le destinataire est déjà validé (FindActiveAssigneeAsync) et n'est jamais
            // déduit du corps de la requête. Si le créateur s'attribue la tâche, aucune
            // notification : il sait déjà qu'elle existe.
            await _notifications.NotifierAssignationAsync(
                tache, ancienResponsableUserId: null, auteurUserId: userId,
                cancellationToken: HttpContext.RequestAborted);

            return CreatedAtAction(nameof(GetTacheProduction), new { id = tache.Id },
                await MapReadAsync(tache));
        }

        /// <summary>
        /// PUT api/TacheProduction/{id} — mise à jour partielle et contrôlée.
        /// L'ownership n'est pas modifiable ici : ni CreatedByUserId, ni AssignedToUserId
        /// ne figurent dans le DTO. L'assignation passe par POST /{id}/Assigner.
        /// </summary>
        [HttpPut("{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult<TacheReadDto>> PutTacheProduction(
            int id,
            [FromBody] UpdateTacheProductionDto dto)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Titre))
                return BadRequest(new { message = "Le titre de la tâche est requis." });

            var priorite = tache.Priorite;
            if (dto.Priorite != null && !Enum.TryParse<PrioriteTache>(dto.Priorite, true, out priorite))
                return BadRequest(new { message = "Priorité invalide." });

            if (dto.CommandeClientId is int commandeId
                && !await _context.CommandesClients.AnyAsync(c => c.Id == commandeId))
                return BadRequest(new { message = "Commande introuvable." });

            tache.Titre = dto.Titre.Trim();
            tache.Description = dto.Description;
            tache.EquipeAssignee = dto.EquipeAssignee;
            tache.CommandeClientId = dto.CommandeClientId;
            tache.DateDebutPrevue = dto.DateDebutPrevue;
            tache.DateFinPrevue = dto.DateFinPrevue;
            tache.DureeEstimeeHeures = dto.DureeEstimeeHeures;
            tache.NotesProgression = dto.NotesProgression;
            if (dto.Priorite != null)
                tache.Priorite = priorite;

            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(await MapReadAsync(tache));
        }

        [HttpPut("{id}/statut")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> UpdateStatut(int id, [FromBody] UpdateStatutDto data)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            if (!Enum.TryParse<StatutTache>(data.Statut, out var statut))
                return BadRequest("Statut invalide");

            tache.Statut = statut;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/equipe")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> AssignerEquipe(int id, [FromBody] AssignerEquipeDto data)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            tache.EquipeAssignee = data.EquipeId;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// POST api/TacheProduction/{id}/Assigner
        /// Le responsable est un Id d'AspNetUsers, jamais un texte libre. Le serveur
        /// vérifie le droit d'assigner, l'existence et l'activité du destinataire.
        /// Une désassignation (null) est autorisée au propriétaire de la tâche.
        /// </summary>
        [HttpPost("{id}/Assigner")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> AssignerTache(int id, [FromBody] AssignerTacheUtilisateurDto dto)
        {
            var userId = _currentUser.UserId!;
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            // Responsable AVANT l'opération : c'est lui, et lui seul, qui doit être prévenu
            // d'une désassignation. Capturé avant l'écriture pour que la règle de
            // notification repose sur l'état réel, pas sur le DTO.
            var ancienResponsable = tache.AssignedToUserId;

            var erreur = await ApplyAssignationAsync(
                tache, dto.AssignedToUserId, userId, desassignerSiVide: true);
            if (erreur != null) return erreur;

            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();

            // Notifie le nouveau responsable (et l'ancien le cas échéant) — idempotent :
            // réassigner deux fois le même responsable ne produit aucune notification.
            await _notifications.NotifierAssignationAsync(
                tache, ancienResponsable, userId, HttpContext.RequestAborted);

            return Ok(new
            {
                message = "Tâche assignée avec succès",
                assignedToUserId = tache.AssignedToUserId,
                responsable = tache.ResponsableAssigne
            });
        }

        /// <summary>
        /// POST api/TacheProduction/{id}/Commencer
        /// Le corps ne porte plus de nom de responsable : le responsable est celui
        /// enregistré dans AssignedToUserId. Accepter un nom libre ici permettrait de
        /// fabriquer un « responsable » qui n'est pas un utilisateur IMS.
        /// </summary>
        [HttpPost("{id}/Commencer")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> CommencerTache(int id)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            tache.Statut = StatutTache.EnCours;
            tache.DateDebutReelle = DateTime.Now;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche commencée avec succès" });
        }

        [HttpPost("{id}/MettreAJourAvancement")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> MettreAJourAvancement(int id, [FromBody] decimal pourcentage)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            tache.PourcentageAvancement = Math.Max(0, Math.Min(100, pourcentage));
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;

            if (tache.PourcentageAvancement >= 100)
            {
                tache.Statut = StatutTache.Termine;
                tache.DateFinReelle = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Avancement mis à jour avec succès" });
        }

        [HttpPost("{id}/Bloquer")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> BloquerTache(int id, [FromBody] string motif)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            tache.Statut = StatutTache.Bloque;
            tache.ProblemesBloques = motif;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche bloquée avec succès" });
        }

        [HttpPost("{id}/Debloquer")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> DebloquerTache(int id)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            tache.Statut = StatutTache.EnCours;
            tache.ProblemesBloques = null;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche débloquée avec succès" });
        }

        [HttpPost("{id}/Terminer")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> TerminerTache(int id, [FromBody] string? notes)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            tache.Statut = StatutTache.Termine;
            tache.DateFinReelle = DateTime.Now;
            tache.PourcentageAvancement = 100;
            tache.NotesProgression = notes;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;

            // Découplage LOT 8 : la clôture automatique de la commande ne relève
            // plus de TacheProduction (module Magasin). Elle dépend désormais du
            // solde comptable qualité : Q = ΣA + ΣB sur TOUS les triplets
            // (OF, Chaîne, Taille) de la commande — effectué après chaque
            // ControleQualite par QualiteService.CloturerCommandeSiSoldee.
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tâche terminée avec succès" });
        }

        [HttpPost("{id}/ModifierPriorite")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> ModifierPriorite(int id, [FromBody] ModifierPrioriteDto data)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            if (!Enum.TryParse<PrioriteTache>(data.Priorite, out var priorite))
                return BadRequest("Priorité invalide");

            tache.Priorite = priorite;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Priorité mise à jour" });
        }

        [HttpPost("{id}/ModifierEcheance")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> ModifierEcheance(int id, [FromBody] ModifierEcheanceDto data)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            tache.DateFinPrevue = data.DateFinPrevue;
            tache.ModifiePar = _currentUser.UserName;
            tache.DateMiseAJour = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Échéance mise à jour" });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> DeleteTacheProduction(int id)
        {
            var tache = await LoadWritableAsync(id);
            if (tache == null) return NotFound();

            _context.TachesProduction.Remove(tache);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ══════════════════════════ Helpers ══════════════════════════

        /// <summary>
        /// Charge une tâche pour ÉCRIRE, en refusant d'emblée toute tâche qui n'est pas
        /// accessible à l'utilisateur courant. null => 404 pour l'appelant.
        /// Utilisé par TOUTES les routes mutantes : c'est le point d'application unique
        /// de la règle d'ownership.
        /// </summary>
        private async Task<TacheProduction?> LoadWritableAsync(int id)
        {
            var userId = _currentUser.UserId!;
            var canAccessAll = await _ownership.CanAccessAllAsync(userId);

            return await _ownership.FindVisibleForWriteAsync(id, userId, canAccessAll);
        }

        /// <summary>
        /// Résout le responsable demandé. Absent/vide => l'utilisateur courant.
        /// Une valeur tierce exige le droit PeutAssignerTaches et un utilisateur actif.
        /// Retourne null si l'assignation a été appliquée, ou une IActionResult d'erreur.
        /// </summary>
        /// <param name="requestedUserId">Destinataire demandé (vide = non précisé).</param>
        /// <param name="currentUserId">Utilisateur authentifié appelant.</param>
        /// <param name="desassignerSiVide">
        /// Distingue les deux usages, car « vide » ne veut pas dire la même chose :
        /// à la CRÉATION, une valeur vide rattache la tâche à son auteur (elle n'a pas
        /// encore de responsable) ; à l'ASSIGNATION, une valeur vide est une demande
        /// explicite de DÉSASSIGNATION, qui doit retirer le responsable au lieu de
        /// transférer la tâche à l'appelant.
        /// </param>
        private async Task<ActionResult?> ApplyAssignationAsync(
            TacheProduction tache,
            string? requestedUserId,
            string currentUserId,
            bool desassignerSiVide)
        {
            if (string.IsNullOrWhiteSpace(requestedUserId))
            {
                if (desassignerSiVide)
                {
                    _ownership.SetAssignee(tache, null);
                    return null;
                }

                // Par défaut : la tâche est rattachée à celui qui la crée.
                var soi = await _ownership.FindActiveAssigneeAsync(currentUserId);
                _ownership.SetAssignee(tache, soi);
                return null;
            }

            if (requestedUserId == currentUserId)
            {
                var soi = await _ownership.FindActiveAssigneeAsync(currentUserId);
                _ownership.SetAssignee(tache, soi);
                return null;
            }

            if (!await _permissions.CanAssignerTachesAsync(currentUserId))
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Vous n'êtes pas autorisé à assigner une tâche à un autre utilisateur." });

            var assignee = await _ownership.FindActiveAssigneeAsync(requestedUserId);
            if (assignee == null)
                return BadRequest(new { message = "Utilisateur destinataire introuvable ou inactif." });

            _ownership.SetAssignee(tache, assignee);
            return null;
        }

        private async Task<TacheReadDto> MapReadAsync(TacheProduction tache)
        {
            var dto = MapShape(tache);

            // Libellés résolus à la demande : évite de charger les comptes IMS
            // à chaque listing.
            dto.Createur = await ResolveDisplayNameAsync(tache.CreatedByUserId);
            dto.Responsable = await ResolveDisplayNameAsync(tache.AssignedToUserId);

            return dto;
        }

        private async Task<List<TacheReadDto>> MapReadAsync(List<TacheProduction> taches)
        {
            if (taches.Count == 0) return [];

            // Résolution groupée : deux requêtes au lieu d'une par tâche.
            var userIds = taches
                .SelectMany(t => new[] { t.CreatedByUserId, t.AssignedToUserId })
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            var noms = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Nom, u.Prenom })
                .ToListAsync();

            var map = noms.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.Prenom) ? u.Nom : $"{u.Prenom} {u.Nom}");

            return taches.Select(t =>
            {
                var dto = MapShape(t);
                if (t.CreatedByUserId != null) dto.Createur = map.GetValueOrDefault(t.CreatedByUserId);
                if (t.AssignedToUserId != null) dto.Responsable = map.GetValueOrDefault(t.AssignedToUserId);
                return dto;
            }).ToList();
        }

        private async Task<string?> ResolveDisplayNameAsync(string? userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;

            var u = await _context.Users
                .Where(x => x.Id == userId)
                .Select(x => new { x.Nom, x.Prenom })
                .FirstOrDefaultAsync();

            if (u == null) return null;
            return string.IsNullOrWhiteSpace(u.Prenom) ? u.Nom : $"{u.Prenom} {u.Nom}";
        }

        private static TacheReadDto MapShape(TacheProduction t) => new()
        {
            Id = t.Id,
            Titre = t.Titre,
            Description = t.Description,
            CommandeClientId = t.CommandeClientId,
            EquipeAssignee = t.EquipeAssignee,
            ResponsableAssigne = t.ResponsableAssigne,
            CreePar = t.CreePar,
            ModifiePar = t.ModifiePar,
            CreatedByUserId = t.CreatedByUserId,
            AssignedToUserId = t.AssignedToUserId,
            Statut = t.Statut,
            Priorite = t.Priorite,
            DateCreation = t.DateCreation,
            DateDebutPrevue = t.DateDebutPrevue,
            DateFinPrevue = t.DateFinPrevue,
            DateDebutReelle = t.DateDebutReelle,
            DateFinReelle = t.DateFinReelle,
            DureeEstimeeHeures = t.DureeEstimeeHeures,
            DureeReelleHeures = t.DureeReelleHeures,
            PourcentageAvancement = t.PourcentageAvancement,
            NotesProgression = t.NotesProgression,
            ProblemesBloques = t.ProblemesBloques,
            DateMiseAJour = t.DateMiseAJour,
            GroupeTacheId = t.GroupeTacheId,
            CommandeClient = t.CommandeClient == null ? null : new TacheCommandeResumeDto
            {
                Id = t.CommandeClient.Id,
                NumeroCommande = t.CommandeClient.NumeroCommande,
                TitreCommande = t.CommandeClient.TitreCommande,
                ClientNom = t.CommandeClient.Client?.Nom
            }
        };

        // ═════════════════════ Groupes de tâches (gabarits) ═════════════════════
        // Un GroupeTache est un MODÈLE répétitif partagé, pas une ressource
        // appartenant à un utilisateur : il reste protégé par la permission de module.
        // En revanche AppliquerGroupe CRÉE des TacheProduction, qui doivent donc être
        // attribuées à l'utilisateur qui les a lancées.

        [HttpGet("Groupes")]
        [RequireModulePermission("taches", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<GroupeTacheDto>>> GetGroupes()
        {
            var groupes = await _context.GroupesTaches
                .Include(g => g.Lignes)
                .Include(g => g.Taches)
                .OrderByDescending(g => g.DateCreation)
                .ToListAsync();

            return Ok(groupes.Select(g => new GroupeTacheDto
            {
                Id = g.Id,
                Nom = g.Nom,
                Description = g.Description,
                EstActif = g.EstActif,
                DateCreation = g.DateCreation,
                Lignes = g.Lignes.OrderBy(l => l.Ordre).Select(l => new GroupeTacheLigneDto
                {
                    Id = l.Id,
                    GroupeTacheId = l.GroupeTacheId,
                    Titre = l.Titre,
                    Description = l.Description,
                    EquipeAssignee = l.EquipeAssignee,
                    ResponsableAssigne = l.ResponsableAssigne,
                    Priorite = (int)l.Priorite,
                    Ordre = l.Ordre,
                    DureeEstimeeHeures = l.DureeEstimeeHeures,
                }).ToList(),
                NombreCommandesAppliquees = g.Taches.Where(t => t.CommandeClientId.HasValue).Select(t => t.CommandeClientId).Distinct().Count(),
                NombreTachesGenerees = g.Taches.Count,
            }).ToList());
        }

        [HttpPost("Groupes")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult<GroupeTache>> CreateGroupe([FromBody] CreateGroupeTacheDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nom))
                return BadRequest(new { message = "Le nom du groupe est requis." });

            var groupe = new GroupeTache { Nom = dto.Nom.Trim(), Description = dto.Description };
            _context.GroupesTaches.Add(groupe);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Groupe créé", id = groupe.Id });
        }

        [HttpPut("Groupes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> UpdateGroupe(int id, [FromBody] UpdateGroupeTacheDto dto)
        {
            var groupe = await _context.GroupesTaches.FindAsync(id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });

            if (dto.Nom != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Nom))
                    return BadRequest(new { message = "Le nom du groupe est requis." });
                groupe.Nom = dto.Nom.Trim();
            }
            if (dto.Description != null) groupe.Description = dto.Description;
            if (dto.EstActif.HasValue) groupe.EstActif = dto.EstActif.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Groupe mis à jour" });
        }

        [HttpDelete("Groupes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> DeleteGroupe(int id)
        {
            var groupe = await _context.GroupesTaches
                .Include(g => g.Lignes)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });

            // Les tâches générées survivent (GroupeTacheId -> SetNull).
            _context.GroupesTaches.Remove(groupe);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Groupe supprimé (tâches générées conservées)" });
        }

        [HttpPost("Groupes/{id}/Lignes")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult> AddLigneGroupe(int id, [FromBody] CreateGroupeTacheLigneDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Titre))
                return BadRequest(new { message = "Le titre de la ligne est requis." });

            var groupe = await _context.GroupesTaches.FindAsync(id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });

            var ordre = dto.Ordre;
            if (ordre <= 0)
                ordre = (await _context.GroupesTachesLignes.MaxAsync(l => (int?)l.Ordre) ?? 0) + 1;

            var ligne = new GroupeTacheLigne
            {
                GroupeTacheId = id,
                Titre = dto.Titre.Trim(),
                Description = dto.Description,
                EquipeAssignee = dto.EquipeAssignee,
                ResponsableAssigne = dto.ResponsableAssigne,
                Priorite = (PrioriteTache)Math.Clamp(dto.Priorite, 0, 3),
                Ordre = ordre,
                DureeEstimeeHeures = dto.DureeEstimeeHeures,
            };
            _context.GroupesTachesLignes.Add(ligne);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne ajoutée au groupe", id = ligne.Id });
        }

        [HttpPut("Lignes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult> UpdateLigneGroupe(int id, [FromBody] UpdateGroupeTacheLigneDto dto)
        {
            var ligne = await _context.GroupesTachesLignes.FindAsync(id);
            if (ligne == null)
                return NotFound(new { message = "Ligne introuvable." });

            if (dto.Titre != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Titre))
                    return BadRequest(new { message = "Le titre de la ligne est requis." });
                ligne.Titre = dto.Titre.Trim();
            }
            if (dto.Description != null) ligne.Description = dto.Description;
            if (dto.EquipeAssignee != null) ligne.EquipeAssignee = dto.EquipeAssignee;
            if (dto.ResponsableAssigne != null) ligne.ResponsableAssigne = dto.ResponsableAssigne;
            if (dto.Priorite.HasValue) ligne.Priorite = (PrioriteTache)Math.Clamp(dto.Priorite.Value, 0, 3);
            if (dto.Ordre.HasValue) ligne.Ordre = dto.Ordre.Value;
            if (dto.DureeEstimeeHeures.HasValue) ligne.DureeEstimeeHeures = dto.DureeEstimeeHeures.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne mise à jour" });
        }

        [HttpDelete("Lignes/{id}")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<IActionResult> DeleteLigneGroupe(int id)
        {
            var ligne = await _context.GroupesTachesLignes.FindAsync(id);
            if (ligne == null)
                return NotFound(new { message = "Ligne introuvable." });

            _context.GroupesTachesLignes.Remove(ligne);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ligne supprimée du groupe" });
        }

        /// <summary>
        /// Applique un groupe à une commande : génère une TacheProduction par ligne.
        /// Les tâches générées appartiennent à l'utilisateur qui applique le groupe
        /// (CreatedByUserId), et sont assignées à lui par défaut. Si la ligne désigne un
        /// responsable par son NOM, on tente de le résoudre vers un utilisateur IMS actif ;
        /// à défaut, la tâche reste celle du créateur et le libellé legacy est conservé.
        /// </summary>
        [HttpPost("Groupes/{id}/Appliquer")]
        [RequireModulePermission("taches", requireWrite: true)]
        public async Task<ActionResult> AppliquerGroupe(int id, [FromBody] AppliquerGroupeTacheDto dto)
        {
            var userId = _currentUser.UserId!;

            var groupe = await _context.GroupesTaches
                .Include(g => g.Lignes)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (groupe == null)
                return NotFound(new { message = "Groupe introuvable." });
            if (!groupe.EstActif)
                return BadRequest(new { message = "Le groupe est inactif : impossible de l'appliquer." });

            var commande = await _context.CommandesClients.FindAsync(dto.CommandeId);
            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });
            if (groupe.Lignes.Count == 0)
                return BadRequest(new { message = "Le groupe ne contient aucune ligne à appliquer." });

            var createur = await _ownership.FindActiveAssigneeAsync(userId);

            var generees = new List<TacheProduction>();
            foreach (var ligne in groupe.Lignes.OrderBy(l => l.Ordre))
            {
                var tache = new TacheProduction
                {
                    Titre = ligne.Titre,
                    Description = ligne.Description,
                    CommandeClientId = dto.CommandeId,
                    EquipeAssignee = ligne.EquipeAssignee,
                    Statut = StatutTache.NonCommence,
                    Priorite = ligne.Priorite,
                    DureeEstimeeHeures = ligne.DureeEstimeeHeures,
                    GroupeTacheId = groupe.Id,

                    // Propriété décidée côté serveur.
                    CreatedByUserId = userId,
                    CreePar = _currentUser.UserName
                };

                var assignee = await ResolveLigneResponsableAsync(ligne.ResponsableAssigne) ?? createur;
                _ownership.SetAssignee(tache, assignee);

                _context.TachesProduction.Add(tache);
                generees.Add(tache);
            }
            await _context.SaveChangesAsync();

            // Même événement qu'une assignation manuelle : chaque tâche confiée à un tiers
            // le prévient. Les tâches restées à l'applicateur ne génèrent rien (auto-notification).
            foreach (var tache in generees)
            {
                await _notifications.NotifierAssignationAsync(
                    tache, ancienResponsableUserId: null, auteurUserId: userId,
                    cancellationToken: HttpContext.RequestAborted);
            }

            return Ok(new
            {
                message = $"{generees.Count} tâche(s) générée(s) pour la commande {commande.NumeroCommande}",
                count = generees.Count,
                ids = generees.Select(t => t.Id).ToList(),
            });
        }

        /// <summary>
        /// Résolution au mieux d'un libellé de responsable vers un utilisateur IMS actif.
        /// AUCUNE affectation arbitraire : sans correspondance, l'appelant reçoit null et
        /// la tâche reste assignée à son créateur.
        /// </summary>
        private async Task<ApplicationUser?> ResolveLigneResponsableAsync(string? libelle)
        {
            if (string.IsNullOrWhiteSpace(libelle)) return null;

            var nom = libelle.Trim();

            return await _context.Users
                .Where(u => u.EstActif
                            && (u.Nom == nom
                                || (u.Prenom != null && (u.Prenom + " " + u.Nom) == nom)
                                || (u.UserName != null && u.UserName == nom)
                                || (u.Nom + " " + (u.Prenom ?? "") == nom)))
                .OrderBy(u => u.Nom)
                .FirstOrDefaultAsync();
        }
    }
}
