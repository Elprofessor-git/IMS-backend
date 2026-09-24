using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Dtos.Qualite;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Contrôle qualité — réception + contrôle en une saisie (conception §4.2-4.4).
    /// Invariant somme : A + R + B = C (≥ 0 chacun), vérifié à la création puis
    /// immuable (pas d'endpoint de modification/suppression : audit trail).
    /// Références : tour 1 plafonné à R1, tours suivants plafonnés à RN+1 (EnvoiRetouche).
    /// Permissions : module « qualite ».
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ControleQualiteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly QualiteService _qualite;

        public ControleQualiteController(ApplicationDbContext context, QualiteService quality)
        {
            _context = context;
            _qualite = quality;
        }

        // ═══════════ Liste filtrable ═══════════

        [HttpGet]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<ControleQualiteDto>>> List(
            [FromQuery] int? commandeId,
            [FromQuery] int? ofId,
            [FromQuery] int? chaineId,
            [FromQuery] string? taille,
            [FromQuery] string? typeControle)
        {
            var query = _context.ControlesQualite.AsQueryable();

            if (ofId.HasValue)
            {
                var of = await _context.OrdresFabrication.FindAsync(ofId.Value);
                if (of == null)
                    return NotFound(new { message = "Ordre de fabrication introuvable." });
                query = query.Where(c => c.OrdreFabricationId == ofId.Value);
            }
            else if (commandeId.HasValue)
            {
                if (commandeId.Value > 0)
                    query = query.Where(c => c.OrdreFabrication != null && c.OrdreFabrication.CommandeId == commandeId.Value);
            }

            if (chaineId.HasValue)
                query = query.Where(c => c.ChaineProductionId == chaineId.Value);

            if (!string.IsNullOrWhiteSpace(taille))
                query = query.Where(c => c.Taille == taille);

            if (!string.IsNullOrWhiteSpace(typeControle) && Enum.TryParse<TypeControle>(typeControle, true, out var tc))
                query = query.Where(c => c.TypeControle == tc);

            var result = await query
                .OrderByDescending(c => c.DateControle)
                .Select(c => new ControleQualiteDto
                {
                    Id = c.Id,
                    OrdreFabricationId = c.OrdreFabricationId,
                    NumeroCommande = c.OrdreFabrication != null ? c.OrdreFabrication.Commande.NumeroCommande : null,
                    ChaineProductionId = c.ChaineProductionId,
                    ChaineNom = c.ChaineProduction != null ? c.ChaineProduction.Nom : null,
                    TypeControle = c.TypeControle.ToString(),
                    ControleParentId = c.ControleParentId,
                    EnvoiRetoucheId = c.EnvoiRetoucheId,
                    Taille = c.Taille,
                    QuantiteControlee = c.QuantiteControlee,
                    QuantiteAcceptee = c.QuantiteAcceptee,
                    QuantiteRetouche = c.QuantiteRetouche,
                    QuantiteRebut = c.QuantiteRebut,
                    DateControle = c.DateControle,
                    EffectuePar = c.EffectuePar,
                    Notes = c.Notes,
                })
                .ToListAsync();

            return Ok(result);
        }

        // ═══════════ Référence de saisie (R1 / RN+1) — §4.4 ═══════════

        [HttpGet("Reference")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<ReferenceQualiteDto>> GetReference(
            [FromQuery] int commandeId,
            [FromQuery] int? chaineId,
            [FromQuery] string taille,
            [FromQuery] string typeControle,
            [FromQuery] int? controleParentId,
            [FromQuery] int? envoiRetoucheId)
        {
            if (string.IsNullOrWhiteSpace(taille))
                return BadRequest(new { message = "La taille est requise." });

            var reference = await _qualite.ReferenceDisponibleAsync(
                commandeId, chaineId, taille, typeControle, controleParentId, envoiRetoucheId);

            if (reference == null)
                return BadRequest(new { message = "Tour de contrôle invalide : un contrôle RetourSousTraitant exige un EnvoiRetouche, un tour 1 exige un contrôle Interne sans parent ni envoi." });

            var solde = await _qualite.CalculerSoldeAsync(new QualiteService.Triplet(commandeId, chaineId, taille));

            return Ok(new ReferenceQualiteDto
            {
                TripletOfId = 0,
                TripletChaineId = chaineId,
                TripletTaille = taille,
                QuantiteExportee = solde.QuantiteExportee,
                QuantiteControlee = solde.QuantiteControleeTotale,
                QuantiteAcceptee = solde.QuantiteAccepteeTotale,
                QuantiteRetouche = solde.QuantiteRetoucheTotale,
                QuantiteRebut = solde.QuantiteRebutTotale,
                EnCours = solde.EnCours,
                ReferenceDisponible = reference.Value,
                EstSolde = solde.EstSolde,
            });
        }

        // ═══════════ Saisie « Réception + Contrôle » ═══════════

        [HttpPost]
        [RequireModulePermission("qualite", requireWrite: true)]
        public async Task<ActionResult<ControleQualiteDto>> CreateControle([FromBody] CreateControleQualiteDto dto)
        {
            // ── Validations de cohérence du tour (4.4) ──
            if (!Enum.TryParse<TypeControle>(dto.TypeControle, true, out var typeControle))
                return BadRequest(new { message = $"Type de contrôle invalide : '{dto.TypeControle}'." });

            var estInterne = typeControle == TypeControle.Interne;
            var estRetour = typeControle == TypeControle.RetourSousTraitant;

            if (estRetour && (!dto.EnvoiRetoucheId.HasValue || !dto.ControleParentId.HasValue))
                return BadRequest(new { message = "Un contrôle RetourSousTraitant exige ControleParentId et EnvoiRetoucheId (tour de cycle)." });

            if (estInterne && dto.EnvoiRetoucheId.HasValue)
                return BadRequest(new { message = "Un contrôle Interne ne peut pas référencer un envoi de retouche." });

            // ── Héritage du triplet pour un contrôle enfant (4.4) ──
            ControleQualite? parent = null;
            if (dto.ControleParentId.HasValue)
            {
                parent = await _context.ControlesQualite.FindAsync(dto.ControleParentId.Value);
                if (parent == null)
                    return NotFound(new { message = "Contrôle parent introuvable." });
                if (dto.OrdreFabricationId != parent.OrdreFabricationId || dto.Taille != parent.Taille)
                    return BadRequest(new { message = "Un contrôle enfant doit hériter de l'OF et de la taille de son parent." });
            }

            EnvoiRetouche? envoi = null;
            if (dto.EnvoiRetoucheId.HasValue)
            {
                envoi = await _context.EnvoisRetouche.FindAsync(dto.EnvoiRetoucheId.Value);
                if (envoi == null)
                    return NotFound(new { message = "Envoi de retouche introuvable." });
                if (!dto.ControleParentId.HasValue || envoi.ControleQualiteId != dto.ControleParentId.Value)
                    return BadRequest(new { message = "Le contrôle enfant doit être lié au contrôle parent de l'envoi re-contrôlé." });
                // Garde 4.4 : mêmes chaînes
                if (parent != null && envoi.ChaineProductionId != parent.ChaineProductionId)
                    return BadRequest(new { message = "L'envoi de retouche ne concerne pas la chaîne du contrôle parent." });
                // Homogénéité avec la chaîne déclarée sur le nouveau contrôle
                if (envoi.ChaineProductionId != dto.ChaineProductionId)
                    return BadRequest(new { message = "La chaîne du contrôle doit être celle de l'envoi de retouche." });
            }

            if (dto.OrdreFabricationId <= 0)
                return BadRequest(new { message = "Un ordre de fabrication est requis pour rattacher le contrôle." });
            var of = await _context.OrdresFabrication.FindAsync(dto.OrdreFabricationId);
            if (of == null)
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            if (dto.ChaineProductionId.HasValue
                && !await _context.ChainesProduction.AnyAsync(cp => cp.Id == dto.ChaineProductionId.Value))
                return BadRequest(new { message = "Chaîne de production introuvable." });

            // ── Invariant de somme : A + R + B = C ──
            var c = dto.QuantiteControlee;
            var a = dto.QuantiteAcceptee;
            var r = dto.QuantiteRetouche;
            var b = dto.QuantiteRebut;
            if (c < 0 || a < 0 || r < 0 || b < 0)
                return BadRequest(new { message = "Les quantités ne peuvent pas être négatives." });
            if (a + r + b != c)
                return BadRequest(new { message = $"Invariant de somme non respecté : Acceptée({a}) + Retouche({r}) + Rebut({b}) doit égaler Contrôlée({c})." });

            // ── Plafond de saisie : QuantiteControlee ≤ référence ──
            var reference = await _qualite.ReferenceDisponibleAsync(
                of.CommandeId, dto.ChaineProductionId, dto.Taille,
                dto.TypeControle, dto.ControleParentId, dto.EnvoiRetoucheId);

            if (reference == null)
                return Conflict(new { message = "Tour de contrôle invalide (référence indisponible)." });

            if (c > reference.Value)
                return Conflict(new
                {
                    message = $"Quantité contrôlée ({c}) supérieure à la référence du tour ({reference.Value}) : pièces non exportées ou déjà re-contrôlées.",
                    reference = reference.Value,
                });

            // ── Lignes de défauts ──
            if (dto.Defauts != null && dto.Defauts.Sum(d => d.Quantite) > a + r)
                return BadRequest(new { message = "La quantité totale des défauts ne peut pas dépasser les pièces non acceptées." });

            var controle = new ControleQualite
            {
                OrdreFabricationId = of.Id,
                ChaineProductionId = dto.ChaineProductionId,
                TypeControle = typeControle,
                ControleParentId = dto.ControleParentId,
                EnvoiRetoucheId = dto.EnvoiRetoucheId,
                Taille = dto.Taille,
                QuantiteControlee = c,
                QuantiteAcceptee = a,
                QuantiteRetouche = r,
                QuantiteRebut = b,
                DateControle = dto.DateControle ?? DateTime.Now,
                EffectuePar = dto.EffectuePar ?? User.Identity?.Name,
                Notes = dto.Notes,
            };
            _context.ControlesQualite.Add(controle);
            await _context.SaveChangesAsync();

            if (dto.Defauts != null)
            {
                foreach (var dl in dto.Defauts)
                {
                    if (dl.Quantite <= 0)
                        continue;
                    _context.ControleQualiteDefautLignes.Add(new ControleQualiteDefautLigne
                    {
                        ControleQualiteId = controle.Id,
                        DefautCodeId = dl.DefautCodeId,
                        Quantite = dl.Quantite,
                        Notes = dl.Notes,
                    });
                }
                await _context.SaveChangesAsync();
            }

            // ── Clôture comptable de la commande si tous les triplets sont soldés ──
            var cloturee = await _qualite.CloturerCommandeSiSoldee(of.CommandeId);

            var resulting = await _context.ControlesQualite
                .Where(x => x.Id == controle.Id)
                .Select(x => new ControleQualiteDto
                {
                    Id = x.Id,
                    OrdreFabricationId = x.OrdreFabricationId,
                    NumeroCommande = x.OrdreFabrication != null ? x.OrdreFabrication.Commande.NumeroCommande : null,
                    ChaineProductionId = x.ChaineProductionId,
                    ChaineNom = x.ChaineProduction != null ? x.ChaineProduction.Nom : null,
                    TypeControle = x.TypeControle.ToString(),
                    ControleParentId = x.ControleParentId,
                    EnvoiRetoucheId = x.EnvoiRetoucheId,
                    Taille = x.Taille,
                    QuantiteControlee = x.QuantiteControlee,
                    QuantiteAcceptee = x.QuantiteAcceptee,
                    QuantiteRetouche = x.QuantiteRetouche,
                    QuantiteRebut = x.QuantiteRebut,
                    DateControle = x.DateControle,
                    EffectuePar = x.EffectuePar,
                    Notes = x.Notes,
                })
                .FirstAsync();

            return Ok(new { message = cloturee ? "Contrôle enregistré — commande clôturée (égalité comptable vérifiée)." : "Contrôle enregistré", controle = resulting });
        }

        // ═══════════ Détail ═══════════

        [HttpGet("{id}")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<ControleQualiteDto>> GetControle(int id)
        {
            var result = await _context.ControlesQualite
                .Where(x => x.Id == id)
                .Select(x => new ControleQualiteDto
                {
                    Id = x.Id,
                    OrdreFabricationId = x.OrdreFabricationId,
                    NumeroCommande = x.OrdreFabrication != null ? x.OrdreFabrication.Commande.NumeroCommande : null,
                    ChaineProductionId = x.ChaineProductionId,
                    ChaineNom = x.ChaineProduction != null ? x.ChaineProduction.Nom : null,
                    TypeControle = x.TypeControle.ToString(),
                    ControleParentId = x.ControleParentId,
                    EnvoiRetoucheId = x.EnvoiRetoucheId,
                    Taille = x.Taille,
                    QuantiteControlee = x.QuantiteControlee,
                    QuantiteAcceptee = x.QuantiteAcceptee,
                    QuantiteRetouche = x.QuantiteRetouche,
                    QuantiteRebut = x.QuantiteRebut,
                    DateControle = x.DateControle,
                    EffectuePar = x.EffectuePar,
                    Notes = x.Notes,
                    Defauts = x.Defauts.Select(d => new ControleQualiteDefautLigneDto
                    {
                        Id = d.Id,
                        DefautCodeId = d.DefautCodeId,
                        DefautCode = d.DefautCode.Code,
                        DefautLibelle = d.DefautCode.Libelle,
                        Quantite = d.Quantite,
                        Notes = d.Notes,
                    }).ToList(),
                })
                .FirstOrDefaultAsync();

            if (result == null)
                return NotFound(new { message = "Contrôle introuvable." });
            return Ok(result);
        }
    }
}