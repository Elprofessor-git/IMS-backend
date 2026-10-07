using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Backend_Gestion_Magasin_API.Services.Coupe;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Ordre de coupe (C2) : un ordre par couple (commande, modèle, couleur), grain
    /// identique à une feuille du classeur de référence. L'écran est proche de la
    /// feuille Excel — en-tête, jusqu'à 8 colonnes de tailles, jusqu'à 9 plans,
    /// résumé, matières — et TOUTE cellule est modifiable.
    ///
    /// Deux rènes stricts, qui font toute la sûreté de l'écran :
    ///   • ce qui se SAISIT se STOCKE (en-tête, tailles, plans, matières) ;
    ///   • ce qui se CALCULE se RECALCULE à la lecture (restes de
    ///     <see cref="MoteurCoupe"/>, résumé, grandeurs de matières) et n'est
    ///     jamais écrit en base. Une surcharge manuelle n'est donc jamais
    ///     écrasée, ni par un recalcul, ni par un second préremplissage.
    ///
    /// Le préremplissage est explicite (POST {id}/Preremplir) : aucune migration,
    /// aucune lecture automatique ne remplit une cellule. Permissions du module
    /// Coupe sur toutes les routes.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdreCoupeController : ControllerBase
    {
        private const int MaxTailles = 8;
        private const int MaxPlans = 9;

        private readonly ApplicationDbContext _context;

        public OrdreCoupeController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static string Normaliser(string? v) =>
            string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim().ToUpperInvariant();

        // ───────────────────────────── Lecture ─────────────────────────────

        /// <summary>Les ordres de coupe d'une commande, dans l'ordre d'affichage.</summary>
        [HttpGet("commande/{commandeId}")]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<IEnumerable<OrdreCoupeListeDto>>> GetListe(int commandeId)
        {
            if (!await _context.CommandesClients.AnyAsync(c => c.Id == commandeId))
                return NotFound(new { message = "Commande introuvable." });

            var ordres = await _context.OrdresCoupe
                .AsNoTracking()
                .Include(o => o.ChaineProduction)
                .Where(o => o.CommandeClientId == commandeId)
                .OrderBy(o => o.Modele)
                .ThenBy(o => o.Couleur)
                .ToListAsync();

            // Résumé de chaque ligne : vite fait en mémoire, le détail n'est pas chargé.
            var ids = ordres.Select(o => o.Id).ToList();
            var tailles = await _context.OrdreCoupeTailles.AsNoTracking()
                .Where(t => ids.Contains(t.OrdreCoupeId)).ToListAsync();
            var plans = await _context.OrdreCoupePlans.AsNoTracking()
                .Where(p => ids.Contains(p.OrdreCoupeId)).ToListAsync();
            var occurrences = await _context.OrdreCoupeOccurrences.AsNoTracking()
                .Where(o => plans.Select(p => p.Id).Contains(o.OrdreCoupePlanId)).ToListAsync();
            var matieres = await _context.OrdreCoupeMatieres.AsNoTracking()
                .Where(m => ids.Contains(m.OrdreCoupeId)).ToListAsync();
            var matelasCoupes = await MatelasCoupesAsync(plans.Select(p => p.MatelasId));

            var dtos = new List<OrdreCoupeListeDto>(ordres.Count);
            foreach (var o in ordres)
            {
                var mesTailles = tailles.Where(t => t.OrdreCoupeId == o.Id).ToList();
                var mesPlans = plans.Where(p => p.OrdreCoupeId == o.Id).ToList();
                var resultat = MoteurCoupe.Calculer(Gabarits(mesTailles), Passe(mesPlans));
                dtos.Add(new OrdreCoupeListeDto
                {
                    Id = o.Id,
                    CommandeId = o.CommandeClientId,
                    Modele = o.Modele,
                    Couleur = o.Couleur,
                    ChaineProductionNom = o.ChaineProduction?.Nom,
                    NbTailles = mesTailles.Count,
                    NbPlans = mesPlans.Count,
                    NbMatieres = matieres.Count(m => m.OrdreCoupeId == o.Id),
                    TotalCommande = resultat.QuantiteCommandee,
                    TotalPlanifie = resultat.TotalPlanifie,
                    AlerteManque = resultat.AlerteManque,
                    Verrouille = mesPlans.Any(p => p.MatelasId.HasValue && matelasCoupes.Contains(p.MatelasId.Value)),
                    DateCreation = o.DateCreation,
                });
            }

            return Ok(dtos);
        }

        /// <summary>
        /// Détail d'un ordre, entièrement recalculé : restes après chaque plan,
        /// résumé (commandé / planifié / surplus / manque) et grandeurs de matières.
        /// </summary>
        [HttpGet("{id}")]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<OrdreCoupeDto>> Get(int id)
        {
            var ordre = await ChargerAsync(id);
            if (ordre == null)
                return NotFound(new { message = "Ordre de coupe introuvable." });

            var commande = await _context.CommandesClients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == ordre.CommandeClientId);

            return Ok(await ConstruireDtoAsync(ordre, commande));
        }

        // ───────────────────────────── En-tête ─────────────────────────────

        /// <summary>
        /// Crée un ordre VIDE : ni tailles, ni quantités, ni marge ne sont devinés.
        /// Le préremplissage est un appel séparé, explicite (Preremplir).
        /// </summary>
        [HttpPost]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> Creer([FromBody] CreerOrdreCoupeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Modele) || string.IsNullOrWhiteSpace(dto.Couleur))
                return BadRequest(new { message = "Modèle et couleur sont requis." });

            var commande = await _context.CommandesClients.FindAsync(dto.CommandeId);
            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });

            if (dto.ChaineProductionId.HasValue &&
                !await _context.ChainesProduction.AnyAsync(c => c.Id == dto.ChaineProductionId.Value))
                return BadRequest(new { message = "Chaîne de production introuvable." });

            if (await DoublonAsync(dto.CommandeId, dto.Modele, dto.Couleur))
                return Conflict(new { message = $"Un ordre de coupe existe déjà pour le couple modèle/couleur « {dto.Modele} / {dto.Couleur} »." });

            var ordre = new OrdreCoupe
            {
                CommandeClientId = dto.CommandeId,
                Modele = dto.Modele.Trim(),
                Couleur = dto.Couleur.Trim(),
                ChaineProductionId = dto.ChaineProductionId,
                MargeSecurite = dto.MargeSecurite,
                ReferenceOF = dto.ReferenceOF,
                Notes = dto.Notes,
                CreePar = User.Identity?.Name,
            };
            _context.OrdresCoupe.Add(ordre);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ordre de coupe créé", id = ordre.Id });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> Modifier(int id, [FromBody] ModifierOrdreCoupeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Modele) || string.IsNullOrWhiteSpace(dto.Couleur))
                return BadRequest(new { message = "Modèle et couleur sont requis." });

            var ordre = await ChargerAsync(id, track: true);
            if (ordre == null)
                return NotFound(new { message = "Ordre de coupe introuvable." });

            if (dto.ChaineProductionId.HasValue &&
                !await _context.ChainesProduction.AnyAsync(c => c.Id == dto.ChaineProductionId.Value))
                return BadRequest(new { message = "Chaîne de production introuvable." });

            if (await DoublonAsync(ordre.CommandeClientId, dto.Modele, dto.Couleur, ordre.Id))
                return Conflict(new { message = $"Un ordre de coupe existe déjà pour le couple modèle/couleur « {dto.Modele} / {dto.Couleur} »." });

            ordre.Modele = dto.Modele.Trim();
            ordre.Couleur = dto.Couleur.Trim();
            ordre.ChaineProductionId = dto.ChaineProductionId;
            ordre.MargeSecurite = dto.MargeSecurite;
            ordre.ReferenceOF = dto.ReferenceOF;
            ordre.Notes = dto.Notes;
            ordre.DateMiseAJour = DateTime.Now;
            ordre.ModifiePar = User.Identity?.Name;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ordre de coupe mis à jour", id = ordre.Id });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> Supprimer(int id)
        {
            var ordre = await ChargerAsync(id, track: true);
            if (ordre == null)
                return NotFound(new { message = "Ordre de coupe introuvable." });

            // Un plan figé porte de l'historique de coupe : on ne détruit pas ce qui
            // a déjà été coupé (même garde-fou que le matelas et son plan).
            var coupes = await MatelasCoupesAsync(ordre.Plans.Select(p => p.MatelasId));
            if (ordre.Plans.Any(p => p.MatelasId.HasValue && coupes.Contains(p.MatelasId.Value)))
                return Conflict(new { message = "Impossible de supprimer un ordre de coupe dont un plan porte déjà des coupes." });

            _context.OrdresCoupe.Remove(ordre);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ordre de coupe supprimé" });
        }

        // ───────────────────────────── Préremplissage explicite ─────────────────────────────

        /// <summary>
        /// Préremplit l'ordre depuis la commande : colonnes de tailles (libellé,
        /// quantité demandée, quantité avec marge) et marge de sécurité.
        ///
        /// Règle de sûreté — SEULEMENT les cellules vides (NULL) sont remplies :
        /// une surcharge manuelle est conservée telle quelle, un second appel ne
        /// peut donc rien écraser. Aucune écriture n'a lieu en dehors de cette route.
        /// </summary>
        [HttpPost("{id}/Preremplir")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult<OrdreCoupeDto>> Preremplir(int id)
        {
            var ordre = await ChargerAsync(id, track: true);
            if (ordre == null)
                return NotFound(new { message = "Ordre de coupe introuvable." });

            var commande = await _context.CommandesClients.AsNoTracking()
                .Include(c => c.ConfigTailles)
                .FirstOrDefaultAsync(c => c.Id == ordre.CommandeClientId);
            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });

            var bilan = new PreremplissageDto();
            var marge = commande.MargeSecuriteDefaut;

            if (ordre.MargeSecurite == null)
            {
                ordre.MargeSecurite = marge;
                bilan.CellulesRemplies++;
            }
            else
            {
                marge = ordre.MargeSecurite.Value;
                bilan.CellulesConservees++;
            }

            var existantes = ordre.Tailles.ToList();
            var indexLibre = existantes.Count == 0 ? 0 : existantes.Max(t => t.Index) + 1;

            foreach (var ct in commande.ConfigTailles.OrderBy(c => c.Id))
            {
                var connue = existantes.FirstOrDefault(t => Normaliser(t.Libelle) == Normaliser(ct.Taille));
                if (connue != null)
                {
                    bilan.TaillesIgnorees++;
                    if (connue.QuantiteDemandee == null)
                    {
                        connue.QuantiteDemandee = ct.Quantite;
                        bilan.CellulesRemplies++;
                    }
                    else
                    {
                        bilan.CellulesConservees++;
                    }

                    if (connue.QuantiteAvecMarge == null)
                    {
                        connue.QuantiteAvecMarge = AvecMarge(ct.Quantite, marge);
                        bilan.CellulesRemplies++;
                    }
                    else
                    {
                        bilan.CellulesConservees++;
                    }

                    continue;
                }

                if (existantes.Count >= MaxTailles)
                {
                    bilan.TaillesIgnorees++;
                    continue;
                }

                var taille = new OrdreCoupeTaille
                {
                    OrdreCoupeId = ordre.Id,
                    Index = indexLibre++,
                    Libelle = ct.Taille,
                    QuantiteDemandee = ct.Quantite,
                    QuantiteAvecMarge = AvecMarge(ct.Quantite, marge),
                };
                ordre.Tailles.Add(taille);
                existantes.Add(taille);
                bilan.TaillesAjoutees++;
                bilan.CellulesRemplies += 2;
            }

            ordre.DateMiseAJour = DateTime.Now;
            ordre.ModifiePar = User.Identity?.Name;
            await _context.SaveChangesAsync();

            bilan.MargeSecurite = ordre.MargeSecurite ?? marge;
            bilan.Message = bilan.TaillesAjoutees == 0 && bilan.CellulesRemplies == 0
                ? "Rien à remplir : toutes les cellules sont déjà renseignées."
                : $"{bilan.TaillesAjoutees} taille(s) ajoutée(s), {bilan.CellulesRemplies} cellule(s) remplie(s), {bilan.CellulesConservees} conservée(s).";

            var recharge = await ChargerAsync(id, track: false);
            var cmd = await _context.CommandesClients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == ordre.CommandeClientId);
            return Ok(await ConstruireDtoAsync(recharge!, cmd));
        }

        private static decimal AvecMarge(int quantite, decimal marge) =>
            Math.Round(quantite * (1m + marge / 100m), 2, MidpointRounding.AwayFromZero);

        // ───────────────────────────── Tailles (colonnes) ─────────────────────────────

        [HttpPost("{id}/tailles")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> AjouterTaille(int id, [FromBody] CreerTailleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Libelle))
                return BadRequest(new { message = "Libellé de taille requis." });

            var ordre = await ChargerAsync(id, track: true);
            if (ordre == null)
                return NotFound(new { message = "Ordre de coupe introuvable." });

            if (ordre.Tailles.Count >= MaxTailles)
                return Conflict(new { message = $"Maximum {MaxTailles} colonnes de tailles par ordre de coupe." });

            if (ordre.Tailles.Any(t => Normaliser(t.Libelle) == Normaliser(dto.Libelle)))
                return Conflict(new { message = $"La taille '{dto.Libelle}' existe déjà sur cet ordre." });

            if (dto.QuantiteDemandee < 0 || dto.QuantiteAvecMarge < 0)
                return BadRequest(new { message = "Les quantités ne peuvent pas être négatives." });

            var taille = new OrdreCoupeTaille
            {
                OrdreCoupeId = ordre.Id,
                Index = ordre.Tailles.Count == 0 ? 0 : ordre.Tailles.Max(t => t.Index) + 1,
                Libelle = dto.Libelle.Trim(),
                QuantiteDemandee = dto.QuantiteDemandee,
                QuantiteAvecMarge = dto.QuantiteAvecMarge,
                Notes = dto.Notes,
            };
            _context.OrdreCoupeTailles.Add(taille);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Taille ajoutée", id = taille.Id });
        }

        [HttpPut("tailles/{tailleId}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> ModifierTaille(int tailleId, [FromBody] ModifierTailleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Libelle))
                return BadRequest(new { message = "Libellé de taille requis." });
            if (dto.QuantiteDemandee < 0 || dto.QuantiteAvecMarge < 0)
                return BadRequest(new { message = "Les quantités ne peuvent pas être négatives." });

            var taille = await _context.OrdreCoupeTailles
                .Include(t => t.OrdreCoupe)
                .ThenInclude(o => o.Tailles)
                .FirstOrDefaultAsync(t => t.Id == tailleId);
            if (taille == null)
                return NotFound(new { message = "Taille introuvable." });

            var ancienLibelle = taille.Libelle;
            var nouveauLibelle = dto.Libelle.Trim();

            if (taille.OrdreCoupe.Tailles.Any(t =>
                    t.Id != taille.Id && Normaliser(t.Libelle) == Normaliser(nouveauLibelle)))
                return Conflict(new { message = $"La taille '{nouveauLibelle}' existe déjà sur cet ordre." });

            // Renommer une colonne renomme aussi les occurrences correspondantes des
            // plans : sinon des cellules de plan resteraient orphelines et sortiraient
            // de l'échelle de restes.
            if (!string.Equals(ancienLibelle, nouveauLibelle, StringComparison.Ordinal))
            {
                var plans = await _context.OrdreCoupePlans
                    .Include(p => p.Occurrences)
                    .Where(p => p.OrdreCoupeId == taille.OrdreCoupeId)
                    .ToListAsync();

                // Un plan ne peut pas porter deux fois la même occurrence : si la
                // nouvelle étiquette y existe déjà, le renommage est refusé.
                foreach (var plan in plans)
                {
                    var futur = plan.Occurrences.Any(o => Normaliser(o.Taille) == Normaliser(nouveauLibelle))
                             && plan.Occurrences.Any(o => Normaliser(o.Taille) == Normaliser(ancienLibelle));
                    if (futur)
                        return Conflict(new { message = $"Impossible de renommer en '{nouveauLibelle}' : un plan contient déjà cette taille." });
                }

                foreach (var occ in plans.SelectMany(p => p.Occurrences))
                    if (Normaliser(occ.Taille) == Normaliser(ancienLibelle))
                        occ.Taille = nouveauLibelle;
            }

            taille.Libelle = nouveauLibelle;
            taille.QuantiteDemandee = dto.QuantiteDemandee;
            taille.QuantiteAvecMarge = dto.QuantiteAvecMarge;
            taille.Notes = dto.Notes;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Taille mise à jour", id = taille.Id });
        }

        [HttpDelete("tailles/{tailleId}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> SupprimerTaille(int tailleId)
        {
            var taille = await _context.OrdreCoupeTailles
                .Include(t => t.OrdreCoupe)
                .FirstOrDefaultAsync(t => t.Id == tailleId);
            if (taille == null)
                return NotFound(new { message = "Taille introuvable." });

            // Les occurrences des plans qui pointent vers cette colonne partent avec
            // elle : la base ne porte aucune clé étrangère taille → plan, le
            // ménage est fait ici, dans la même transaction.
            var plans = await _context.OrdreCoupePlans
                .Include(p => p.Occurrences)
                .Where(p => p.OrdreCoupeId == taille.OrdreCoupeId)
                .ToListAsync();
            var occurrences = plans
                .SelectMany(p => p.Occurrences)
                .Where(o => Normaliser(o.Taille) == Normaliser(taille.Libelle))
                .ToList();
            _context.OrdreCoupeOccurrences.RemoveRange(occurrences);

            _context.OrdreCoupeTailles.Remove(taille);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Taille supprimée", occurrencesRetirees = occurrences.Count });
        }

        // ───────────────────────────── Plans (matelas) ─────────────────────────────

        [HttpPost("{id}/plans")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> AjouterPlan(int id, [FromBody] CreerPlanDto dto)
        {
            var ordre = await ChargerAsync(id, track: true);
            if (ordre == null)
                return NotFound(new { message = "Ordre de coupe introuvable." });

            if (ordre.Plans.Count >= MaxPlans)
                return Conflict(new { message = $"Maximum {MaxPlans} plans par ordre de coupe." });

            var erreur = await ValiderPlanAsync(ordre, dto.Libelle, dto.Plis, dto.MatelasId, dto.Occurrences, planId: null);
            if (erreur != null)
                return erreur;

            var plan = new OrdreCoupePlan
            {
                OrdreCoupeId = ordre.Id,
                Index = ordre.Plans.Count == 0 ? 0 : ordre.Plans.Max(p => p.Index) + 1,
                Libelle = dto.Libelle,
                Plis = dto.Plis,
                MatelasId = dto.MatelasId,
            };
            foreach (var occ in dto.Occurrences)
                plan.Occurrences.Add(new OrdreCoupePlanOccurrence { Taille = occ.Taille.Trim(), Occurrences = occ.Occurrences });

            _context.OrdreCoupePlans.Add(plan);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan ajouté", id = plan.Id });
        }

        [HttpPut("plans/{planId}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> ModifierPlan(int planId, [FromBody] ModifierPlanDto dto)
        {
            var plan = await _context.OrdreCoupePlans
                .Include(p => p.OrdreCoupe).ThenInclude(o => o.Tailles)
                .Include(p => p.Occurrences)
                .FirstOrDefaultAsync(p => p.Id == planId);
            if (plan == null)
                return NotFound(new { message = "Plan introuvable." });

            var erreur = await ValiderPlanAsync(plan.OrdreCoupe, dto.Libelle, dto.Plis, dto.MatelasId, dto.Occurrences, planId);
            if (erreur != null)
                return erreur;

            plan.Libelle = dto.Libelle;
            plan.Plis = dto.Plis;
            plan.MatelasId = dto.MatelasId;

            _context.OrdreCoupeOccurrences.RemoveRange(plan.Occurrences);
            plan.Occurrences.Clear();
            foreach (var occ in dto.Occurrences)
                plan.Occurrences.Add(new OrdreCoupePlanOccurrence { Taille = occ.Taille.Trim(), Occurrences = occ.Occurrences });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Plan mis à jour", id = plan.Id });
        }

        [HttpDelete("plans/{planId}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> SupprimerPlan(int planId)
        {
            var plan = await _context.OrdreCoupePlans
                .Include(p => p.Occurrences)
                .FirstOrDefaultAsync(p => p.Id == planId);
            if (plan == null)
                return NotFound(new { message = "Plan introuvable." });

            if (plan.MatelasId.HasValue && await PlanVerrouilleAsync(plan.MatelasId.Value))
                return VerrouErreur();

            _context.OrdreCoupePlans.Remove(plan);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Plan supprimé" });
        }

        // ───────────────────────────── Matières ─────────────────────────────

        [HttpPost("{id}/matieres")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> AjouterMatiere(int id, [FromBody] CreerMatiereDto dto)
        {
            var erreur = ValiderMatiere(dto.Designation, dto.ConsoClient, dto.ConsoReelle, dto.RetraitPourcentage, dto.MetresRecus, dto.Laize);
            if (erreur != null) return erreur;

            var ordre = await _context.OrdresCoupe.FindAsync(id);
            if (ordre == null)
                return NotFound(new { message = "Ordre de coupe introuvable." });

            var matiere = new OrdreCoupeMatiere
            {
                OrdreCoupeId = ordre.Id,
                Designation = dto.Designation.Trim(),
                Laize = dto.Laize,
                ConsoClient = dto.ConsoClient,
                ConsoReelle = dto.ConsoReelle,
                RetraitPourcentage = dto.RetraitPourcentage,
                MetresRecus = dto.MetresRecus,
                Notes = dto.Notes,
            };
            _context.OrdreCoupeMatieres.Add(matiere);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Matière ajoutée", id = matiere.Id });
        }

        [HttpPut("matieres/{matiereId}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> ModifierMatiere(int matiereId, [FromBody] ModifierMatiereDto dto)
        {
            var erreur = ValiderMatiere(dto.Designation, dto.ConsoClient, dto.ConsoReelle, dto.RetraitPourcentage, dto.MetresRecus, dto.Laize);
            if (erreur != null) return erreur;

            var matiere = await _context.OrdreCoupeMatieres.FindAsync(matiereId);
            if (matiere == null)
                return NotFound(new { message = "Matière introuvable." });

            matiere.Designation = dto.Designation.Trim();
            matiere.Laize = dto.Laize;
            matiere.ConsoClient = dto.ConsoClient;
            matiere.ConsoReelle = dto.ConsoReelle;
            matiere.RetraitPourcentage = dto.RetraitPourcentage;
            matiere.MetresRecus = dto.MetresRecus;
            matiere.Notes = dto.Notes;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Matière mise à jour", id = matiere.Id });
        }

        [HttpDelete("matieres/{matiereId}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> SupprimerMatiere(int matiereId)
        {
            var matiere = await _context.OrdreCoupeMatieres.FindAsync(matiereId);
            if (matiere == null)
                return NotFound(new { message = "Matière introuvable." });

            _context.OrdreCoupeMatieres.Remove(matiere);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Matière supprimée" });
        }

        private static ActionResult? ValiderMatiere(string? designation, decimal consoClient, decimal? consoReelle,
            decimal retrait, decimal recus, decimal? laize)
        {
            if (string.IsNullOrWhiteSpace(designation))
                return new BadRequestObjectResult(new { message = "Désignation requise." });
            if (consoClient < 0 || (consoReelle.HasValue && consoReelle.Value < 0))
                return new BadRequestObjectResult(new { message = "Consommation négative." });
            if (retrait < 0 || retrait > 100)
                return new BadRequestObjectResult(new { message = "Le retrait doit être compris entre 0 et 100 %." });
            if (recus < 0)
                return new BadRequestObjectResult(new { message = "Les mètres reçus ne peuvent pas être négatifs." });
            if (laize.HasValue && laize.Value < 0)
                return new BadRequestObjectResult(new { message = "Laize négative." });
            return null;
        }

        // ───────────────────────────── Verrous & validations ─────────────────────────────

        /// <summary>
        /// Verrou existant, inchangé : un plan est figé dès que des coupes réelles
        /// sont rattachées à son matelas (409 sur toute écriture de plan).
        /// </summary>
        private async Task<bool> PlanVerrouilleAsync(int matelasId) =>
            await _context.LotCoupes.AnyAsync(l => l.MatelasId == matelasId);

        private static ConflictObjectResult VerrouErreur() =>
            new(new { message = "Plan figé : le matelas rattaché porte déjà des coupes enregistrées." });

        private async Task<ActionResult?> ValiderPlanAsync(
            OrdreCoupe ordre,
            string? libelle,
            int plis,
            int? matelasId,
            List<OccurrenceDto>? occurrences,
            int? planId)
        {
            if (plis < 1)
                return BadRequest(new { message = "Le nombre de plis doit être au moins égal à 1." });

            occurrences ??= new List<OccurrenceDto>();

            // Verrou : le plan actuel est figé, ou la cible du rattachement l'est déjà.
            var actuel = planId.HasValue
                ? await _context.OrdreCoupePlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId.Value)
                : null;
            if (actuel?.MatelasId.HasValue == true && await PlanVerrouilleAsync(actuel.MatelasId.Value))
                return VerrouErreur();
            if (matelasId.HasValue && await PlanVerrouilleAsync(matelasId.Value))
                return VerrouErreur();

            if (matelasId.HasValue &&
                !await _context.Matelas.AnyAsync(m => m.Id == matelasId.Value
                    && (m.CommandeId == null || m.CommandeId == ordre.CommandeClientId)))
                return BadRequest(new { message = "Matelas introuvable ou hors commande." });

            var tailles = ordre.Tailles.Select(t => Normaliser(t.Libelle)).ToHashSet();
            foreach (var occ in occurrences)
            {
                if (string.IsNullOrWhiteSpace(occ.Taille))
                    return BadRequest(new { message = "Taille requise dans le plan." });
                if (occ.Occurrences < 1)
                    return BadRequest(new { message = "Occurrences ≥ 1." });
                if (!tailles.Contains(Normaliser(occ.Taille)))
                    return BadRequest(new { message = $"Taille '{occ.Taille}' absente des colonnes de l'ordre de coupe." });
            }

            var dupliquat = occurrences
                .GroupBy(o => Normaliser(o.Taille))
                .FirstOrDefault(g => g.Count() > 1);
            if (dupliquat != null)
                return Conflict(new { message = $"La taille '{dupliquat.First().Taille}' apparaît deux fois dans ce plan." });

            return null;
        }

        private async Task<bool> DoublonAsync(int commandeId, string modele, string couleur, int? exclu = null)
        {
            // Comparaison en mémoire, sur les mêmes formes normalisées que la couche
            // service : la casse et les espaces ne doivent pas créer deux ordres.
            var candidats = await _context.OrdresCoupe.AsNoTracking()
                .Where(o => o.CommandeClientId == commandeId && (exclu == null || o.Id != exclu.Value))
                .Select(o => new { o.Modele, o.Couleur })
                .ToListAsync();

            return candidats.Any(o =>
                Normaliser(o.Modele) == Normaliser(modele) && Normaliser(o.Couleur) == Normaliser(couleur));
        }

        // ───────────────────────────── Construction de la vue ─────────────────────────────

        private async Task<OrdreCoupe?> ChargerAsync(int id, bool track = false)
        {
            var query = _context.OrdresCoupe
                .Include(o => o.ChaineProduction)
                .Include(o => o.Tailles)
                .Include(o => o.Plans).ThenInclude(p => p.Occurrences)
                .Include(o => o.Plans).ThenInclude(p => p.Matelas)
                .Include(o => o.Matieres)
                .Where(o => o.Id == id);

            if (!track) query = query.AsNoTracking();
            return await query.FirstOrDefaultAsync();
        }

        private async Task<OrdreCoupeDto> ConstruireDtoAsync(OrdreCoupe ordre, CommandeClient? commande)
        {
            var tailles = ordre.Tailles.OrderBy(t => t.Index).ThenBy(t => t.Id).ToList();
            var plans = ordre.Plans.OrderBy(p => p.Index).ThenBy(p => p.Id).ToList();

            // Le reste n'existe pas ailleurs que dans ce calcul : ni colonne, ni
            // historique. Le moteur C1 est la seule source de la règle.
            var resultat = MoteurCoupe.Calculer(Gabarits(tailles), Passe(plans));
            var coupes = await MatelasCoupesAsync(plans.Select(p => p.MatelasId));

            var marge = ordre.MargeSecurite ?? commande?.MargeSecuriteDefaut ?? 0m;
            var dto = new OrdreCoupeDto
            {
                Id = ordre.Id,
                CommandeId = ordre.CommandeClientId,
                NumeroCommande = commande?.NumeroCommande ?? string.Empty,
                Modele = ordre.Modele,
                Couleur = ordre.Couleur,
                ChaineProductionId = ordre.ChaineProductionId,
                ChaineProductionNom = ordre.ChaineProduction?.Nom,
                MargeSecurite = marge,
                MargePersonnalisee = ordre.MargeSecurite != null,
                ReferenceOF = ordre.ReferenceOF,
                Notes = ordre.Notes,
                DateCreation = ordre.DateCreation,
                DateMiseAJour = ordre.DateMiseAJour,
                TotalCommande = resultat.QuantiteCommandee,
                TotalPlanifie = resultat.TotalPlanifie,
                Surplus = resultat.Surplus,
                Manque = resultat.Manque,
                AlerteManque = resultat.AlerteManque,
            };

            foreach (var t in tailles)
            {
                dto.Tailles.Add(new OrdreCoupeTailleDto
                {
                    Id = t.Id,
                    Index = t.Index,
                    Libelle = t.Libelle,
                    QuantiteDemandee = t.QuantiteDemandee,
                    QuantiteAvecMarge = t.QuantiteAvecMarge,
                    Notes = t.Notes,
                });
            }

            for (var i = 0; i < plans.Count; i++)
            {
                var p = plans[i];
                var ligne = resultat.Passes[i];
                var verrouille = p.MatelasId.HasValue && coupes.Contains(p.MatelasId.Value);
                dto.Verrouille |= verrouille;

                var planDto = new OrdreCoupePlanDto
                {
                    Id = p.Id,
                    Index = p.Index,
                    Libelle = p.Libelle,
                    Plis = p.Plis,
                    MatelasId = p.MatelasId,
                    MatelasNumero = p.Matelas?.NumeroMatelas,
                    Verrouille = verrouille,
                    Occurrences = p.Occurrences
                        .OrderBy(o => TailleIndex(tailles, o.Taille))
                        .Select(o => new OccurrenceDto { Taille = o.Taille, Occurrences = o.Occurrences })
                        .ToList(),
                };

                // Restes dans l'ordre des colonnes : c'est la ligne qu'on lit sous
                // le plan. Un reste négatif est conservé tel quel (surplus).
                foreach (var t in tailles)
                    planDto.Restes.Add(new OrdreCoupeResteDto
                    {
                        Taille = t.Libelle,
                        Valeur = ligne.Restes.TryGetValue(t.Libelle, out var reste) ? reste : 0,
                    });

                dto.Plans.Add(planDto);
            }

            foreach (var m in ordre.Matieres.OrderBy(m => m.Id))
            {
                var utilises = Math.Round(resultat.TotalPlanifie * m.ConsoClient, 2, MidpointRounding.AwayFromZero);
                var retrait = Math.Round(m.RetraitPourcentage / 100m * m.MetresRecus, 2, MidpointRounding.AwayFromZero);
                var stock = Math.Round(m.MetresRecus - utilises - retrait, 2, MidpointRounding.AwayFromZero);

                dto.Matieres.Add(new OrdreCoupeMatiereDto
                {
                    Id = m.Id,
                    Designation = m.Designation,
                    Laize = m.Laize,
                    ConsoClient = m.ConsoClient,
                    ConsoReelle = m.ConsoReelle,
                    EcartConso = m.ConsoReelle.HasValue
                        ? Math.Round(m.ConsoReelle.Value - m.ConsoClient, 2, MidpointRounding.AwayFromZero)
                        : null,
                    RetraitPourcentage = m.RetraitPourcentage,
                    MetresRecus = m.MetresRecus,
                    Notes = m.Notes,
                    Utilises = utilises,
                    Retrait = retrait,
                    StockRestant = stock,
                    Alerte = stock < 0,
                });
            }

            return dto;
        }

        private static int TailleIndex(List<OrdreCoupeTaille> tailles, string valeur) =>
            Math.Max(0, tailles.FindIndex(t => Normaliser(t.Libelle) == Normaliser(valeur)));

        private static List<Gabarit> Gabarits(List<OrdreCoupeTaille> tailles) =>
            tailles.Select(t => new Gabarit(t.Libelle, t.QuantiteDemandee ?? 0)).ToList();

        private static List<PasseCoupe> Passe(List<OrdreCoupePlan> plans) =>
            plans
                .Select((p, i) => new PasseCoupe(
                    i + 1,
                    p.Plis,
                    p.Occurrences
                        .Where(o => o.Occurrences > 0)
                        .ToDictionary(o => o.Taille, o => o.Occurrences, StringComparer.OrdinalIgnoreCase),
                    p.Libelle))
                .ToList();

        /// <summary>Matelas déjà servis par une coupe réelle (verrou existant).</summary>
        private async Task<HashSet<int>> MatelasCoupesAsync(IEnumerable<int?> matelasIds)
        {
            var ids = matelasIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            if (ids.Count == 0) return new HashSet<int>();

            var coupes = await _context.LotCoupes.AsNoTracking()
                .Where(l => l.MatelasId != null && ids.Contains(l.MatelasId.Value))
                .Select(l => l.MatelasId!.Value)
                .Distinct()
                .ToListAsync();
            return new HashSet<int>(coupes);
        }
    }
}
