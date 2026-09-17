using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Ordres de fabrication — Phase 1 (structure + étiquetage).
    /// Couche additive scoping CommandeClient : ne touche ni Calculer ni
    /// ValiderRessources (aucune lecture de dimension OF dans le calcul).
    /// Permission : module « commandes » (réutilisation, Décision 4).
    /// Aucune méthode protégée n'est touchée.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdreFabricationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OrdreFabricationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ═══════════ Listage par commande ═══════════

        [HttpGet("CommandeClient/{commandeId}")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<OrdreFabricationDto>>> GetOrdresDeCommande(int commandeId)
        {
            if (!await _context.CommandesClients.AnyAsync(c => c.Id == commandeId))
                return NotFound(new { message = "Commande introuvable." });

            var ordres = await _context.OrdresFabrication
                .Where(of => of.CommandeId == commandeId)
                .OrderBy(of => of.NumeroOF)
                .Select(of => new OrdreFabricationDto
                {
                    Id = of.Id,
                    CommandeId = of.CommandeId,
                    NumeroOF = of.NumeroOF,
                    ChaineProductionId = of.ChaineProductionId,
                    ChaineProductionNom = of.ChaineProduction != null ? of.ChaineProduction.Nom : null,
                    DateCreation = of.DateCreation,
                    Notes = of.Notes,
                    TotalPieces = of.Tailles.Sum(t => t.Quantite),
                    NombreLignesTailles = of.Tailles.Count,
                    NombreEtiquettes = of.Etiquettes.Count,
                })
                .ToListAsync();
            return Ok(ordres);
        }

        // ═══════════ Détail ═══════════

        [HttpGet("{id}")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<OrdreFabricationDetailDto>> GetOrdreFabrication(int id)
        {
            var dto = await _context.OrdresFabrication
                .Where(of => of.Id == id)
                .Select(of => new OrdreFabricationDetailDto
                {
                    Id = of.Id,
                    CommandeId = of.CommandeId,
                    NumeroOF = of.NumeroOF,
                    ChaineProductionId = of.ChaineProductionId,
                    ChaineProductionNom = of.ChaineProduction != null ? of.ChaineProduction.Nom : null,
                    DateCreation = of.DateCreation,
                    Notes = of.Notes,
                    TotalPieces = of.Tailles.Sum(t => t.Quantite),
                    NombreLignesTailles = of.Tailles.Count,
                    NombreEtiquettes = of.Etiquettes.Count,
                    Matelas = of.Commande.LotCoupes
                        .Where(lc => lc.OrdreFabricationId == of.Id && lc.MatelasId != null)
                        .Select(lc => new OrdreFabricationMatelasDto
                        {
                            Id = lc.Matelas!.Id,
                            NumeroMatelas = lc.Matelas.NumeroMatelas,
                            DateMatelas = lc.Matelas.DateMatelas,
                            PiecePliage = lc.Matelas.PiecePliage,
                            CoupeEstimee = lc.Matelas.CoupeEstimee,
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            if (dto == null)
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            // Cumul des quantités coupées par matelas rattaché à cet OF
            foreach (var m in dto.Matelas)
            {
                var qte = await _context.LotCoupes
                    .Where(lc => lc.OrdreFabricationId == id && lc.MatelasId == m.Id)
                    .SumAsync(lc => (int?)lc.QuantiteCoupee) ?? 0;
                m.QuantiteCoupee = qte;
            }

            dto.Tailles = await _context.OrdresFabricationTailles
                .Where(t => t.OrdreFabricationId == id)
                .OrderBy(t => t.Taille)
                .Select(t => new OrdreFabricationTailleDto
                {
                    Id = t.Id,
                    OrdreFabricationId = t.OrdreFabricationId,
                    Taille = t.Taille,
                    Quantite = t.Quantite,
                })
                .ToListAsync();

            dto.Etiquettes = await _context.OrdresFabricationEtiquettes
                .Where(e => e.OrdreFabricationId == id)
                .OrderByDescending(e => e.DateImpression)
                .Select(e => new OrdreFabricationEtiquetteDto
                {
                    Id = e.Id,
                    OrdreFabricationId = e.OrdreFabricationId,
                    FournitureCommandeLigneId = e.FournitureCommandeLigneId,
                    FournitureDesignation = e.FournitureLigne != null ? e.FournitureLigne.DesignationSpecifique : null,
                    Taille = e.Taille,
                    QuantiteEtiquettes = e.QuantiteEtiquettes,
                    DateImpression = e.DateImpression,
                    EffectuePar = e.EffectuePar,
                    Notes = e.Notes,
                })
                .ToListAsync();

            return Ok(dto);
        }

        // ═══════════ Création ═══════════

        [HttpPost]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult<OrdreFabricationWriteResponse>> CreateOrdreFabrication([FromBody] CreateOrdreFabricationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NumeroOF))
                return BadRequest(new { message = "Le numéro d'OF est requis." });

            if (!await _context.CommandesClients.AnyAsync(c => c.Id == dto.CommandeId))
                return NotFound(new { message = "Commande introuvable." });

            // Unicité composée (CommandeId, NumeroOF) — Décision 5
            if (await _context.OrdresFabrication.AnyAsync(of => of.CommandeId == dto.CommandeId && of.NumeroOF == dto.NumeroOF.Trim()))
                return Conflict(new { message = $"Un OF '{dto.NumeroOF}' existe déjà pour cette commande." });

            if (dto.ChaineProductionId.HasValue &&
                !await _context.ChainesProduction.AnyAsync(cp => cp.Id == dto.ChaineProductionId.Value))
                return BadRequest(new { message = "Chaîne de production introuvable." });

            var ordre = new OrdreFabrication
            {
                CommandeId = dto.CommandeId,
                NumeroOF = dto.NumeroOF.Trim(),
                ChaineProductionId = dto.ChaineProductionId,
                DateCreation = DateTime.Now,
                Notes = dto.Notes,
                CreePar = User.Identity?.Name,
            };
            _context.OrdresFabrication.Add(ordre);
            await _context.SaveChangesAsync();

            return Ok(new OrdreFabricationWriteResponse
            {
                Message = "Ordre de fabrication créé",
                Id = ordre.Id,
                AvertissementCohérence = await AvertissementCohérenceAsync(dto.CommandeId),
            });
        }

        // ═══════════ Mise à jour ═══════════

        [HttpPut("{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult<OrdreFabricationWriteResponse>> UpdateOrdreFabrication(int id, [FromBody] UpdateOrdreFabricationDto dto)
        {
            var ordre = await _context.OrdresFabrication.FindAsync(id);
            if (ordre == null)
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            if (dto.NumeroOF != null)
            {
                if (string.IsNullOrWhiteSpace(dto.NumeroOF))
                    return BadRequest(new { message = "Le numéro d'OF est requis." });
                var numero = dto.NumeroOF.Trim();
                if (await _context.OrdresFabrication.AnyAsync(of => of.CommandeId == ordre.CommandeId && of.NumeroOF == numero && of.Id != id))
                    return Conflict(new { message = $"Un OF '{dto.NumeroOF}' existe déjà pour cette commande." });
                ordre.NumeroOF = numero;
            }

            if (dto.ChaineProductionId.HasValue)
            {
                if (!await _context.ChainesProduction.AnyAsync(cp => cp.Id == dto.ChaineProductionId.Value))
                    return BadRequest(new { message = "Chaîne de production introuvable." });
                ordre.ChaineProductionId = dto.ChaineProductionId;
            }

            if (dto.Notes != null)
                ordre.Notes = dto.Notes;

            ordre.DateMiseAJour = DateTime.Now;
            ordre.ModifiePar = User.Identity?.Name;
            await _context.SaveChangesAsync();

            return Ok(new OrdreFabricationWriteResponse
            {
                Message = "Ordre de fabrication mis à jour",
                Id = ordre.Id,
                AvertissementCohérence = await AvertissementCohérenceAsync(ordre.CommandeId),
            });
        }

        // ═══════════ Suppression ═══════════

        [HttpDelete("{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> DeleteOrdreFabrication(int id)
        {
            var ordre = await _context.OrdresFabrication.FindAsync(id);
            if (ordre == null)
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            // Suppression physique : lignes de tailles et étiquettes supprimées en cascade ;
            // les coupes (LotCoupe) gardent leur historique (OrdreFabricationId -> NULL, SetNull).
            _context.OrdresFabrication.Remove(ordre);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Ordre de fabrication supprimé" });
        }

        // ═══════════ Répartition par taille (remplacement, pattern SetTailles) ═══════════

        [HttpPost("{id}/Tailles")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> SetOrdreFabricationTailles(int id, [FromBody] List<SaisieOrdreFabricationTailleDto> dtos)
        {
            var ordre = await _context.OrdresFabrication.FindAsync(id);
            if (ordre == null)
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            if (dtos.Any(d => string.IsNullOrWhiteSpace(d.Taille)))
                return BadRequest(new { message = "Chaque ligne de répartition doit avoir une taille." });

            var existants = _context.OrdresFabricationTailles.Where(t => t.OrdreFabricationId == id);
            _context.OrdresFabricationTailles.RemoveRange(existants);

            foreach (var d in dtos)
            {
                _context.OrdresFabricationTailles.Add(new OrdreFabricationTailleLigne
                {
                    OrdreFabricationId = id,
                    Taille = d.Taille.Trim(),
                    Quantite = d.Quantite,
                });
            }
            await _context.SaveChangesAsync();

            var total = dtos.Sum(d => d.Quantite);
            return Ok(new OrdreFabricationWriteResponse
            {
                Message = "Répartition enregistrée",
                Id = id,
                AvertissementCohérence = await AvertissementCohérenceAsync(ordre.CommandeId),
            });
        }

        // ═══════════ Étiquettes ═══════════

        [HttpPost("{id}/Etiquettes")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> CreateEtiquette(int id, [FromBody] CreateOrdreFabricationEtiquetteDto dto)
        {
            var ordre = await _context.OrdresFabrication.FindAsync(id);
            if (ordre == null)
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            if (dto.QuantiteEtiquettes <= 0)
                return BadRequest(new { message = "La quantité d'étiquettes doit être > 0." });

            if (dto.FournitureCommandeLigneId.HasValue)
            {
                var ligne = await _context.FournitureCommandesLignes
                    .FirstOrDefaultAsync(f => f.Id == dto.FournitureCommandeLigneId.Value && f.CommandeId == ordre.CommandeId);
                if (ligne == null)
                    return BadRequest(new { message = "Ligne de fourniture introuvable ou hors commande." });
            }

            var etiquette = new OrdreFabricationEtiquette
            {
                OrdreFabricationId = id,
                FournitureCommandeLigneId = dto.FournitureCommandeLigneId,
                Taille = dto.Taille,
                QuantiteEtiquettes = dto.QuantiteEtiquettes,
                DateImpression = dto.DateImpression ?? DateTime.Now,
                EffectuePar = dto.EffectuePar ?? User.Identity?.Name,
                Notes = dto.Notes,
            };
            _context.OrdresFabricationEtiquettes.Add(etiquette);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Étiquette enregistrée", id = etiquette.Id });
        }

        // ═══════════ Matelas rattachés (lecture via LotCoupes) ═══════════

        [HttpGet("{id}/Matelas")]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<OrdreFabricationMatelasDto>>> GetMatelasDeOrdreFabrication(int id)
        {
            if (!await _context.OrdresFabrication.AnyAsync(of => of.Id == id))
                return NotFound(new { message = "Ordre de fabrication introuvable." });

            var matelas = await _context.LotCoupes
                .Where(lc => lc.OrdreFabricationId == id && lc.MatelasId != null)
                .Select(lc => new OrdreFabricationMatelasDto
                {
                    Id = lc.Matelas!.Id,
                    NumeroMatelas = lc.Matelas.NumeroMatelas,
                    DateMatelas = lc.Matelas.DateMatelas,
                    PiecePliage = lc.Matelas.PiecePliage,
                    CoupeEstimee = lc.Matelas.CoupeEstimee,
                })
                .Distinct()
                .ToListAsync();

            foreach (var m in matelas)
            {
                var qte = await _context.LotCoupes
                    .Where(lc => lc.OrdreFabricationId == id && lc.MatelasId == m.Id)
                    .SumAsync(lc => (int?)lc.QuantiteCoupee) ?? 0;
                m.QuantiteCoupee = qte;
            }

            return Ok(matelas);
        }

        // ═══════════ Helpers ═══════════

        /// <summary>
        /// Option B (Q1) : saisies indépendantes en parallèle. Avertissement NON bloquant
        /// si la somme des répartitions de TOUS les OF de la commande diffère de la
        /// somme des ConfigTailles (commande). Le calcul reste intact.
        /// </summary>
        private async Task<string?> AvertissementCohérenceAsync(int commandeId)
        {
            var totalCommande = await _context.ConfigTailles
                .Where(ct => ct.CommandeId == commandeId)
                .SumAsync(ct => (decimal)ct.Quantite);

            var totalOfs = await _context.OrdresFabrication
                .Where(of => of.CommandeId == commandeId)
                .Select(of => (decimal?)of.Tailles.Sum(t => t.Quantite))
                .SumAsync() ?? 0;

            if (totalCommande <= 0)
                return "La commande n'a pas de répartition par taille : les OF sont saisis indépendamment (le calcul restera au niveau commande).";

            if (totalOfs != totalCommande)
                return $"La répartition cumulée des OF ({totalOfs} pièces) ne correspond pas à celle de la commande ({totalCommande} pièces). Incohérence non bloquante — le calcul de couverture reste au niveau commande.";

            return null;
        }
    }
}