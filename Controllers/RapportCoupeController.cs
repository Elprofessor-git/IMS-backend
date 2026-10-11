using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Services.Coupe;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RapportCoupeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ExcelExportService _excel;
        private readonly PdfExportService _pdf;
        /// <summary>Moteur unique (C3) : rapport et ordre de coupe partagent le même calcul.</summary>
        private readonly RapportCoupeService _moteur;

        public RapportCoupeController(ApplicationDbContext context, ExcelExportService excel, PdfExportService pdf)
        {
            _context = context;
            _excel = excel;
            _pdf = pdf;
            _moteur = new RapportCoupeService(context);
        }

        // ───────────────────────────── Rapport complet ─────────────────────────────

        [HttpGet("{commandeId}")]
        [RequireModulePermission("commandes,coupe")]
        public async Task<ActionResult<RapportCoupeDto>> GetRapportCoupe(int commandeId)
        {
            var dto = await BuildRapportCoupeAsync(commandeId);
            if (dto == null)
                return NotFound(new { message = "Commande introuvable." });

            return Ok(dto);
        }

        // Export Excel : remplit le gabarit « RAP DE COUPE ».
        [HttpGet("{commandeId}/Export")]
        [RequireModulePermission("commandes,coupe")]
        public async Task<IActionResult> ExportRapportCoupe(int commandeId)
        {
            var dto = await BuildRapportCoupeAsync(commandeId);
            if (dto == null)
                return NotFound(new { message = "Commande introuvable." });

            var bytes = _excel.ExportRapportCoupe(dto);
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"RapportCoupe_{dto.NumeroCommande}.xlsx");
        }

        // Export PDF (QuestPDF) — même préparation de données que l'export Excel.
        [HttpGet("{commandeId}/ExportPdf")]
        [RequireModulePermission("commandes,coupe")]
        public async Task<IActionResult> ExportRapportCoupePdf(int commandeId)
        {
            var dto = await BuildRapportCoupeAsync(commandeId);
            if (dto == null)
                return NotFound(new { message = "Commande introuvable." });

            var bytes = _pdf.ExportRapportCoupe(dto);
            return File(
                bytes,
                "application/pdf",
                $"RapportCoupe_{dto.NumeroCommande}.pdf");
        }

        private async Task<RapportCoupeDto?> BuildRapportCoupeAsync(int commandeId)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.Client)
                .Include(c => c.BomLignes)
                    .ThenInclude(b => b.Article)
                .FirstOrDefaultAsync(c => c.Id == commandeId);

            if (commande == null)
                return null;

            // Groupe de la commande (si elle est rattachée)
            var groupeId = await _context.GroupeCommandeCommandes
                .Where(gcc => gcc.CommandeClientId == commandeId)
                .Select(gcc => (int?)gcc.GroupeCommandeId)
                .FirstOrDefaultAsync();

            // ── Les cumuls ne vivent qu'au moteur unique (C3) : plan théorique, coupes
            //    réelles, exports, seuil de marge. Aucune zone de calcul indépendante.
            var calcul = await _moteur.CalculerAsync(commandeId);
            if (calcul == null)
                return null;

            var dto = new RapportCoupeDto
            {
                CommandeId = commande.Id,
                NumeroCommande = commande.NumeroCommande,
                TitreCommande = commande.TitreCommande,
                ClientNom = commande.Client?.Nom,
                TotalQuantiteCommande = calcul.TotalQuantiteCommande,
                TotalQuantiteCoupee = calcul.TotalCoupesToutes,
                TotalQuantiteExportee = calcul.TotalExportee,
                TotalPlanTheorique = calcul.TotalPlanTheorique,
            };

            // ── Totaux par taille ──
            foreach (var t in calcul.Tailles)
            {
                dto.Tailles.Add(new RapportCoupeTailleDto
                {
                    Taille = t.Taille,
                    QuantiteCommande = t.QuantiteCommande,
                    PlanTheorique = t.PlanTheorique,
                    QuantiteCoupee = t.CoupeReelle,
                    QuantiteExportee = t.Exportee,
                    DepassementCoupe = t.DepasseCoupe,
                    DepassePlan = t.DepassePlan,
                    DepassementExport = t.DepasseExport,
                });
            }

            // ── Tissus consommables (BOM) avec métrage annoncé et restant ──
            foreach (var b in commande.BomLignes.Where(bl => bl.EstConsommableTissu))
            {
                var article = b.Article;
                var ligne = dto.Tissus.FirstOrDefault(t => t.ArticleId == b.ArticleId);
                if (ligne == null)
                {
                    ligne = new RapportCoupeTissuDto
                    {
                        ArticleId = b.ArticleId,
                        Designation = article?.Designation ?? $"#{b.ArticleId}",
                        Laize = article?.Laize,
                        MetrageAnnonce = await CalculerMetrageAnnonceAsync(b.ArticleId, commandeId, groupeId),
                        ConsoReelle = b.QuantiteParPiece,
                    };
                    dto.Tissus.Add(ligne);
                }
                else
                {
                    ligne.ConsoReelle += b.QuantiteParPiece;
                }
            }

            foreach (var t in dto.Tissus)
            {
                t.QuantiteCoupee = dto.TotalQuantiteCoupee;
                t.MetrageReelle = Math.Round(t.ConsoReelle * t.QuantiteCoupee, 2);
                t.StockRestant = Math.Round(t.MetrageAnnonce - t.MetrageReelle, 2);
            }

            return dto;
        }

        private async Task<decimal> CalculerMetrageAnnonceAsync(int articleId, int commandeId, int? groupeId)
        {
            var q = _context.Stocks
                .Where(s => s.ArticleId == articleId
                    && s.EstValide
                    && s.TypeStock == TypeStock.Importe
                    && (s.CommandeClientId == commandeId
                        || (groupeId != null && s.GroupeCommandeId == groupeId)));

            return await q.SumAsync(s => (decimal?)s.Quantite) ?? 0m;
        }

        // ───────────────────────────── Ordre de coupe document (L2) ─────────────────────────────
        // Le plan de coupe agrégé devient la référence atelier : pour la commande, compare
        // le cumul des plans (Σ Occurrences × PiecePliage par matelas) à la demande ConfigTaille
        // augmentée de la marge de sécurité. Vue calculée, aucune table de planning.

        [HttpGet("{commandeId}/OrdreDeCoupe")]
        [RequireModulePermission("commandes,coupe")]
        public async Task<ActionResult<OrdreDeCoupeDto>> GetOrdreDeCoupe(int commandeId)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.ConfigTailles)
                .FirstOrDefaultAsync(c => c.Id == commandeId);

            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });

            // Le plan de coupe passe par le moteur commun — mais désormais la lecture
            // du rapport ET de l'ordre de coupe passent par LE MÊME service (C3) : les
            // deux vues dérivent du même résultat, elles ne peuvent pas diverger.
            var calcul = await _moteur.CalculerAsync(commandeId);
            if (calcul == null)
                return NotFound(new { message = "Commande introuvable." });

            var dto = new OrdreDeCoupeDto
            {
                CommandeId = commande.Id,
                NumeroCommande = commande.NumeroCommande,
                MargeSecuriteDefaut = commande.MargeSecuriteDefaut,
            };

            for (var i = 0; i < calcul.Matelas.Count; i++)
            {
                var mc = calcul.Matelas[i];
                var plan = new OrdreCoupeMatelasDto
                {
                    MatelasId = mc.MatelasId,
                    NumeroMatelas = mc.NumeroMatelas,
                    DateMatelas = mc.DateMatelas,
                    PiecePliage = mc.PiecePliage,
                    Longueur = mc.Longueur,
                    Laize = mc.Laize,
                    OrdreDeCoupe = mc.OrdreDeCoupe,
                    TotalTheorique = mc.TotalTheorique,
                    TotalCoupeReelle = mc.TotalCoupeReelle,
                    ResteTotal = mc.ResteTotal,
                };

                foreach (var ligne in mc.Lignes)
                {
                    plan.Lignes.Add(new OrdreCoupePlanLigneDto
                    {
                        LigneId = ligne.LigneId,
                        MatelasId = ligne.MatelasId,
                        Taille = ligne.Taille,
                        Occurrences = ligne.Occurrences,
                        Theorique = ligne.Theorique,
                        CoupeReelle = ligne.CoupeReelle,
                        ResteACouper = ligne.ResteACouper,
                    });
                }

                dto.TotalPlanTheorique += plan.TotalTheorique;
                dto.TotalCoupeReelle += plan.TotalCoupeReelle;
                dto.Matelas.Add(plan);
            }
            dto.TotalCoupesSansMatelas = calcul.TotalCoupesSansMatelas;

            // Couverture par taille : un seul cumul en mémoire, celui du moteur.
            foreach (var ct in commande.ConfigTailles.OrderBy(c => c.Taille).ToList())
            {
                var cumul = calcul.Tailles.FirstOrDefault(t => t.Taille == ct.Taille);
                if (cumul == null) continue;
                dto.Tailles.Add(new OrdreCoupeTailleRapportDto
                {
                    Taille = cumul.Taille,
                    QuantiteCommande = cumul.QuantiteCommande,
                    Seuil = Math.Round(cumul.Seuil, 2, MidpointRounding.AwayFromZero),
                    PlanTheorique = cumul.PlanTheorique,
                    CoupeReelle = cumul.CoupeReelle,
                    DepassePlan = cumul.DepassePlan,
                    DepasseCoupe = cumul.DepasseCoupe,
                    CoupesSansMatelas = cumul.CoupesSansMatelas,
                });
            }

            return Ok(dto);
        }

        // ───────────────────────────── Entrées de coupe ─────────────────────────────

        [HttpGet("{commandeId}/Coupes")]
        [RequireModulePermission("commandes,coupe")]
        public async Task<ActionResult<IEnumerable<LotCoupeDto>>> GetCoupes(int commandeId)
        {
            var coupes = await _context.LotCoupes
                .Where(l => l.CommandeId == commandeId)
                .OrderByDescending(l => l.DateCoupe)
                .Select(l => new LotCoupeDto
                {
                    Id = l.Id,
                    CommandeId = l.CommandeId,
                    Taille = l.Taille,
                    QuantiteCoupee = l.QuantiteCoupee,
                    DateCoupe = l.DateCoupe,
                    EffectuePar = l.EffectuePar,
                    ForcerDepassement = l.ForcerDepassement,
                    Notes = l.Notes,
                    OrdreFabricationId = l.OrdreFabricationId,
                    OrdreFabricationNumero = l.OrdreFabrication != null ? l.OrdreFabrication.NumeroOF : null,
                    MatelasId = l.MatelasId,
                    MatelasNumero = l.Matelas != null ? l.Matelas.NumeroMatelas : null,
                    OrdreCoupeId = l.OrdreCoupeId,
                })
                .ToListAsync();
            return Ok(coupes);
        }

        [HttpPost("{commandeId}/Coupes")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> AjouterCoupe(int commandeId, [FromBody] CreateLotDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Taille) || dto.QuantiteCoupee <= 0)
                return BadRequest(new { message = "Taille requise et quantité coupée > 0." });

            var commande = await _context.CommandesClients
                .Include(c => c.ConfigTailles)
                .FirstOrDefaultAsync(c => c.Id == commandeId);
            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });

            var configTaille = commande.ConfigTailles
                .FirstOrDefault(ct => ct.Taille == dto.Taille);
            if (configTaille == null)
                return BadRequest(new { message = $"Taille '{dto.Taille}' absente de la configuration de la commande." });

            if (dto.OrdreFabricationId.HasValue &&
                !await _context.OrdresFabrication.AnyAsync(of => of.Id == dto.OrdreFabricationId.Value && of.CommandeId == commandeId))
                return BadRequest(new { message = "Ordre de fabrication introuvable ou hors commande." });

            // Un matelas partagé/historique (CommandeId NULL) reste valide et
            // peut être rattaché à une coupe de n'importe quelle commande.
            if (dto.MatelasId.HasValue &&
                !await _context.Matelas.AnyAsync(m => m.Id == dto.MatelasId.Value && (m.CommandeId == null || m.CommandeId == commandeId)))
                return BadRequest(new { message = "Matelas introuvable ou hors commande." });

            // Ordre de coupe (C3) : soit fourni, soit déduit du matelas rattaché.
            // Toujours cohérent avec la commande — jamais hors scope.
            var rattache = await ResoudreOrdreCoupeIdAsync(commandeId, dto.OrdreCoupeId, dto.MatelasId);
            if (!rattache.Succes)
                return BadRequest(new { message = "Ordre de coupe introuvable ou hors commande." });

            var totalExistant = await _context.LotCoupes
                .Where(l => l.CommandeId == commandeId && l.Taille == dto.Taille)
                .SumAsync(l => (int?)l.QuantiteCoupee) ?? 0;

            var total = totalExistant + dto.QuantiteCoupee;

            // Dépassement toléré jusqu'à la marge de sécurité par défaut de la commande :
            // seuil = Quantite commandée × (1 + MargeSecuriteDefaut / 100).
            var seuilDepassement = configTaille.Quantite * (1m + commande.MargeSecuriteDefaut / 100m);
            if (total > seuilDepassement && !dto.ForcerDepassement)
                return Conflict(new
                {
                    message = $"Dépassement de coupe: {total} > quantité commandée {configTaille.Quantite} (marge {commande.MargeSecuriteDefaut}% => seuil {seuilDepassement:0.#}) pour la taille '{dto.Taille}'. Cochez « forcer le dépassement » pour enregistrer quand même.",
                    taille = dto.Taille,
                    quantiteCommande = configTaille.Quantite,
                    totalCoupe = total,
                    seuilDepassement = seuilDepassement,
                    margeSecurite = commande.MargeSecuriteDefaut,
                });

            var coupe = new LotCoupe
            {
                CommandeId = commandeId,
                Taille = dto.Taille,
                QuantiteCoupee = dto.QuantiteCoupee,
                DateCoupe = DateTime.Now,
                EffectuePar = User.Identity?.Name,
                ForcerDepassement = dto.ForcerDepassement,
                Notes = dto.Notes,
                OrdreFabricationId = dto.OrdreFabricationId,
                MatelasId = dto.MatelasId,
                OrdreCoupeId = rattache.OrdreCoupeId,
            };
            _context.LotCoupes.Add(coupe);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Coupe enregistrée",
                id = coupe.Id,
                totalTaille = total
            });
        }

        [HttpPut("{commandeId}/Coupes/{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> ModifierCoupe(int commandeId, int id, [FromBody] CreateLotDto dto)
        {
            // Garde-fous identiques au POST AjouterCoupe (l.201-270).
            if (string.IsNullOrWhiteSpace(dto.Taille) || dto.QuantiteCoupee <= 0)
                return BadRequest(new { message = "Taille requise et quantité coupée > 0." });

            var coupe = await _context.LotCoupes
                .FirstOrDefaultAsync(l => l.Id == id && l.CommandeId == commandeId);
            if (coupe == null)
                return NotFound(new { message = "Coupe introuvable ou hors commande." });

            var commande = await _context.CommandesClients
                .Include(c => c.ConfigTailles)
                .FirstOrDefaultAsync(c => c.Id == commandeId);
            if (commande == null)
                return NotFound(new { message = "Commande introuvable." });

            var configTaille = commande.ConfigTailles
                .FirstOrDefault(ct => ct.Taille == dto.Taille);
            if (configTaille == null)
                return BadRequest(new { message = $"Taille '{dto.Taille}' absente de la configuration de la commande." });

            if (dto.OrdreFabricationId.HasValue &&
                !await _context.OrdresFabrication.AnyAsync(of => of.Id == dto.OrdreFabricationId.Value && of.CommandeId == commandeId))
                return BadRequest(new { message = "Ordre de fabrication introuvable ou hors commande." });

            // Un matelas partagé/historique (CommandeId NULL) reste valide.
            if (dto.MatelasId.HasValue &&
                !await _context.Matelas.AnyAsync(m => m.Id == dto.MatelasId.Value && (m.CommandeId == null || m.CommandeId == commandeId)))
                return BadRequest(new { message = "Matelas introuvable ou hors commande." });

            // Ordre de coupe (C3) : déduit du matelas ré-attaché quand le client n'en
            // fournit pas ; validation hors scope interdite.
            var rattache = await ResoudreOrdreCoupeIdAsync(commandeId, dto.OrdreCoupeId, dto.MatelasId);
            if (!rattache.Succes)
                return BadRequest(new { message = "Ordre de coupe introuvable ou hors commande." });

            // Dépassement recalculé hors de la coupe modifiée elle-même.
            var totalExistant = await _context.LotCoupes
                .Where(l => l.CommandeId == commandeId && l.Taille == dto.Taille && l.Id != id)
                .SumAsync(l => (int?)l.QuantiteCoupee) ?? 0;

            var total = totalExistant + dto.QuantiteCoupee;

            var seuilDepassement = configTaille.Quantite * (1m + commande.MargeSecuriteDefaut / 100m);
            if (total > seuilDepassement && !dto.ForcerDepassement)
                return Conflict(new
                {
                    message = $"Dépassement de coupe: {total} > quantité commandée {configTaille.Quantite} (marge {commande.MargeSecuriteDefaut}% => seuil {seuilDepassement:0.#}) pour la taille '{dto.Taille}'. Cochez « forcer le dépassement » pour enregistrer quand même.",
                    taille = dto.Taille,
                    quantiteCommande = configTaille.Quantite,
                    totalCoupe = total,
                    seuilDepassement = seuilDepassement,
                    margeSecurite = commande.MargeSecuriteDefaut,
                });

            coupe.Taille = dto.Taille;
            coupe.QuantiteCoupee = dto.QuantiteCoupee;
            coupe.ForcerDepassement = dto.ForcerDepassement;
            if (dto.Notes != null) coupe.Notes = dto.Notes;
            coupe.OrdreFabricationId = dto.OrdreFabricationId;
            coupe.MatelasId = dto.MatelasId;
            coupe.OrdreCoupeId = rattache.OrdreCoupeId;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Coupe mise à jour",
                id = coupe.Id,
                totalTaille = total
            });
        }

        [HttpDelete("Coupes/{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> SupprimerCoupe(int id)
        {
            var coupe = await _context.LotCoupes.FindAsync(id);
            if (coupe == null)
                return NotFound(new { message = "Coupe introuvable." });
            _context.LotCoupes.Remove(coupe);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Coupe supprimée" });
        }

        // ───────────────────────────── Entrées d'export (atelier) ─────────────────────────────

        [HttpGet("{commandeId}/Exports")]
        [RequireModulePermission("commandes,coupe")]
        public async Task<ActionResult<IEnumerable<LotExportDto>>> GetExports(int commandeId)
        {
            var exports = await _context.LotExports
                .Where(l => l.CommandeId == commandeId)
                .OrderByDescending(l => l.DateExport)
                .Select(l => new LotExportDto
                {
                    Id = l.Id,
                    CommandeId = l.CommandeId,
                    Taille = l.Taille,
                    QuantiteExportee = l.QuantiteExportee,
                    DateExport = l.DateExport,
                    EffectuePar = l.EffectuePar,
                    ForcerDepassement = l.ForcerDepassement,
                    Notes = l.Notes,
                    ChaineProductionId = l.ChaineProductionId,
                    ChaineProductionNom = l.ChaineProduction != null ? l.ChaineProduction.Nom : null,
                })
                .ToListAsync();
            return Ok(exports);
        }

        [HttpPost("{commandeId}/Exports")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> AjouterExport(int commandeId, [FromBody] CreateLotDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Taille) || dto.QuantiteExportee <= 0)
                return BadRequest(new { message = "Taille requise et quantité exportée > 0." });

            var commande = await _context.CommandesClients.AnyAsync(c => c.Id == commandeId);
            if (!commande)
                return NotFound(new { message = "Commande introuvable." });

            if (dto.ChaineProductionId.HasValue &&
                !await _context.ChainesProduction.AnyAsync(cp => cp.Id == dto.ChaineProductionId.Value))
                return BadRequest(new { message = "Chaîne de production introuvable." });

            var totalCoupé = await _context.LotCoupes
                .Where(l => l.CommandeId == commandeId && l.Taille == dto.Taille)
                .SumAsync(l => (int?)l.QuantiteCoupee) ?? 0;

            var totalExporté = await _context.LotExports
                .Where(l => l.CommandeId == commandeId && l.Taille == dto.Taille)
                .SumAsync(l => (int?)l.QuantiteExportee) ?? 0;

            var total = totalExporté + dto.QuantiteExportee;
            if (total > totalCoupé && !dto.ForcerDepassement)
                return Conflict(new
                {
                    message = $"Dépassement d'export: {total} > quantité coupée {totalCoupé} pour la taille '{dto.Taille}'. Cochez « forcer le dépassement » pour enregistrer quand même.",
                    taille = dto.Taille,
                    totalCoupe = totalCoupé,
                    totalExport = total
                });

            var export = new LotExport
            {
                CommandeId = commandeId,
                Taille = dto.Taille,
                QuantiteExportee = dto.QuantiteExportee,
                DateExport = DateTime.Now,
                EffectuePar = User.Identity?.Name,
                ForcerDepassement = dto.ForcerDepassement,
                Notes = dto.Notes,
                ChaineProductionId = dto.ChaineProductionId,
            };
            _context.LotExports.Add(export);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Export enregistré",
                id = export.Id,
                totalTaille = total
            });
        }

        [HttpDelete("Exports/{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> SupprimerExport(int id)
        {
            var export = await _context.LotExports.FindAsync(id);
            if (export == null)
                return NotFound(new { message = "Export introuvable." });
            _context.LotExports.Remove(export);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Export supprimé" });
        }

        // ───────────────────────────── Moteur unique / rattachement (C3) ─────────────────────────────

        /// <summary>
        /// Résout l'ordre de coupe d'une coupe : celui fourni par le client, sinon celui
        /// du matelas rattaché. Vérifie qu'il appartient bien à la commande concernée.
        /// </summary>
        private async Task<(bool Succes, int? OrdreCoupeId)> ResoudreOrdreCoupeIdAsync(
            int commandeId, int? dtoOrdreCoupeId, int? matelasId)
        {
            var ordre = dtoOrdreCoupeId;
            if (ordre == null && matelasId.HasValue)
            {
                ordre = await _context.Matelas
                    .AsNoTracking()
                    .Where(m => m.Id == matelasId.Value)
                    .Select(m => (int?)m.OrdreCoupeId)
                    .FirstOrDefaultAsync();
            }
            if (ordre.HasValue &&
                !await _context.OrdresCoupe.AnyAsync(o => o.Id == ordre.Value && o.CommandeClientId == commandeId))
                return (false, null);
            return (true, ordre);
        }
    }
}