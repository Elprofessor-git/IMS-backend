using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Backend_Gestion_Magasin_API.Services;
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

        public RapportCoupeController(ApplicationDbContext context, ExcelExportService excel, PdfExportService pdf)
        {
            _context = context;
            _excel = excel;
            _pdf = pdf;
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
                .Include(c => c.ConfigTailles)
                .Include(c => c.BomLignes)
                    .ThenInclude(b => b.Article)
                .Include(c => c.LotCoupes)
                .Include(c => c.LotExports)
                .FirstOrDefaultAsync(c => c.Id == commandeId);

            if (commande == null)
                return null;

            // Groupe de la commande (si elle est rattachée)
            var groupeId = await _context.GroupeCommandeCommandes
                .Where(gcc => gcc.CommandeClientId == commandeId)
                .Select(gcc => (int?)gcc.GroupeCommandeId)
                .FirstOrDefaultAsync();

            var dto = new RapportCoupeDto
            {
                CommandeId = commande.Id,
                NumeroCommande = commande.NumeroCommande,
                TitreCommande = commande.TitreCommande,
                ClientNom = commande.Client?.Nom,
            };

            // ── Totaux par taille ──
            var coupesParTaille = commande.LotCoupes.GroupBy(l => l.Taille)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.QuantiteCoupee));
            var exportsParTaille = commande.LotExports.GroupBy(l => l.Taille)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.QuantiteExportee));

            foreach (var ct in commande.ConfigTailles.OrderBy(c => c.Taille))
            {
                var coup = coupesParTaille.GetValueOrDefault(ct.Taille);
                var exp = exportsParTaille.GetValueOrDefault(ct.Taille);
                // Dépassement toléré jusqu'à la marge de sécurité par défaut de la commande.
                var seuilCoupe = ct.Quantite * (1m + commande.MargeSecuriteDefaut / 100m);
                dto.Tailles.Add(new RapportCoupeTailleDto
                {
                    Taille = ct.Taille,
                    QuantiteCommande = ct.Quantite,
                    QuantiteCoupee = coup,
                    QuantiteExportee = exp,
                    DepassementCoupe = coup > seuilCoupe,
                    DepassementExport = exp > coup,
                });
                dto.TotalQuantiteCommande += ct.Quantite;
                dto.TotalQuantiteCoupee += coup;
                dto.TotalQuantiteExportee += exp;
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

            // Matelas de la commande chargés séparément (nav unidirectionnelle) avec leur plan.
            var matelas = await _context.Matelas
                .Where(m => m.CommandeId == commandeId)
                .Include(m => m.PlanDeCoupeLignes)
                .Include(m => m.LotCoupes)
                .ToListAsync();

            var dto = new OrdreDeCoupeDto
            {
                CommandeId = commande.Id,
                NumeroCommande = commande.NumeroCommande,
                MargeSecuriteDefaut = commande.MargeSecuriteDefaut,
            };

            // Ordre de coupe = rang dans la séquence (DateMatelas, NumeroMatelas) croissant.
            var matelasTries = matelas
                .OrderBy(m => m.DateMatelas).ThenBy(m => m.NumeroMatelas)
                .ToList();

            // Cumul plan par taille + coupes réelles par taille (commande entière).
            var planParTaille = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var coupeParTaille = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sansMatelasParTaille = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var lotsCommandes = await _context.LotCoupes
                .Where(l => l.CommandeId == commandeId)
                .ToListAsync();
            foreach (var lot in lotsCommandes)
            {
                coupeParTaille[lot.Taille] = coupeParTaille.GetValueOrDefault(lot.Taille) + lot.QuantiteCoupee;
                if (lot.MatelasId == null)
                    sansMatelasParTaille[lot.Taille] = sansMatelasParTaille.GetValueOrDefault(lot.Taille) + lot.QuantiteCoupee;
            }

            for (var i = 0; i < matelasTries.Count; i++)
            {
                var m = matelasTries[i];
                var plan = new OrdreCoupeMatelasDto
                {
                    MatelasId = m.Id,
                    NumeroMatelas = m.NumeroMatelas,
                    DateMatelas = m.DateMatelas,
                    PiecePliage = m.PiecePliage,
                    Longueur = m.Longueur,
                    Laize = m.Laize,
                    OrdreDeCoupe = i + 1,
                };

                var coupesMatelas = m.LotCoupes.Sum(lc => (int?)lc.QuantiteCoupee) ?? 0;
                plan.TotalCoupeReelle = coupesMatelas;

                foreach (var ligne in m.PlanDeCoupeLignes.OrderBy(p => p.Taille))
                {
                    var theorique = ligne.Occurrences * m.PiecePliage;
                    // Coupes réelles de CE matelas sur CETTE taille (vue lecture, calculée).
                    var coupeReelle = m.LotCoupes
                        .Where(lc => string.Equals(lc.Taille, ligne.Taille, StringComparison.OrdinalIgnoreCase))
                        .Sum(lc => (int?)lc.QuantiteCoupee) ?? 0;
                    var reste = theorique - coupeReelle;
                    plan.Lignes.Add(new OrdreCoupePlanLigneDto
                    {
                        LigneId = ligne.Id,
                        MatelasId = m.Id,
                        Taille = ligne.Taille,
                        Occurrences = ligne.Occurrences,
                        Theorique = theorique,
                        CoupeReelle = coupeReelle,
                        ResteACouper = reste,
                    });
                    plan.TotalTheorique += theorique;
                    plan.ResteTotal += reste;
                    planParTaille[ligne.Taille] = planParTaille.GetValueOrDefault(ligne.Taille) + theorique;
                }

                dto.TotalPlanTheorique += plan.TotalTheorique;
                dto.TotalCoupeReelle += coupesMatelas;
                dto.Matelas.Add(plan);
            }

            foreach (var ct in commande.ConfigTailles.OrderBy(c => c.Taille))
            {
                var planT = planParTaille.GetValueOrDefault(ct.Taille);
                var coupeT = coupeParTaille.GetValueOrDefault(ct.Taille);
                var seuil = ct.Quantite * (1m + commande.MargeSecuriteDefaut / 100m);
                dto.Tailles.Add(new OrdreCoupeTailleDto
                {
                    Taille = ct.Taille,
                    QuantiteCommande = ct.Quantite,
                    Seuil =  Math.Round(seuil, 2, MidpointRounding.AwayFromZero),
                    PlanTheorique = planT,
                    CoupeReelle = coupeT,
                    DepassePlan = planT > seuil,
                    DepasseCoupe = coupeT > seuil,
                    CoupesSansMatelas = sansMatelasParTaille.GetValueOrDefault(ct.Taille),
                });
                dto.TotalCoupesSansMatelas += sansMatelasParTaille.GetValueOrDefault(ct.Taille);
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
    }
}