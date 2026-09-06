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
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<RapportCoupeDto>> GetRapportCoupe(int commandeId)
        {
            var dto = await BuildRapportCoupeAsync(commandeId);
            if (dto == null)
                return NotFound(new { message = "Commande introuvable." });

            return Ok(dto);
        }

        // Export Excel : remplit le gabarit « RAP DE COUPE ».
        [HttpGet("{commandeId}/Export")]
        [RequireModulePermission("commandes", requireWrite: false)]
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
        [RequireModulePermission("commandes", requireWrite: false)]
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
                dto.Tailles.Add(new RapportCoupeTailleDto
                {
                    Taille = ct.Taille,
                    QuantiteCommande = ct.Quantite,
                    QuantiteCoupee = coup,
                    QuantiteExportee = exp,
                    DepassementCoupe = coup > ct.Quantite,
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

        // ───────────────────────────── Entrées de coupe ─────────────────────────────

        [HttpGet("{commandeId}/Coupes")]
        [RequireModulePermission("commandes", requireWrite: false)]
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
                })
                .ToListAsync();
            return Ok(coupes);
        }

        [HttpPost("{commandeId}/Coupes")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> AjouterCoupe(int commandeId, [FromBody] CreateLotDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Taille) || dto.QuantiteCoupee <= 0)
                return BadRequest(new { message = "Taille requise et quantité coupée > 0." });

            var commande = await _context.CommandesClients
                .Include(c => c.ConfigTailles)
                .AnyAsync(c => c.Id == commandeId);
            if (!commande)
                return NotFound(new { message = "Commande introuvable." });

            var configTaille = await _context.ConfigTailles
                .FirstOrDefaultAsync(ct => ct.CommandeId == commandeId && ct.Taille == dto.Taille);
            if (configTaille == null)
                return BadRequest(new { message = $"Taille '{dto.Taille}' absente de la configuration de la commande." });

            var totalExistant = await _context.LotCoupes
                .Where(l => l.CommandeId == commandeId && l.Taille == dto.Taille)
                .SumAsync(l => (int?)l.QuantiteCoupee) ?? 0;

            var total = totalExistant + dto.QuantiteCoupee;
            if (total > configTaille.Quantite && !dto.ForcerDepassement)
                return Conflict(new
                {
                    message = $"Dépassement de coupe: {total} > quantite commandée {configTaille.Quantite} pour la taille '{dto.Taille}'. Cochez « forcer le dépassement » pour enregistrer quand même.",
                    taille = dto.Taille,
                    quantiteCommande = configTaille.Quantite,
                    totalCoupe = total
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

        [HttpDelete("Coupes/{id}")]
        [RequireModulePermission("commandes", requireWrite: true)]
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
        [RequireModulePermission("commandes", requireWrite: false)]
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
                })
                .ToListAsync();
            return Ok(exports);
        }

        [HttpPost("{commandeId}/Exports")]
        [RequireModulePermission("commandes", requireWrite: true)]
        public async Task<ActionResult> AjouterExport(int commandeId, [FromBody] CreateLotDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Taille) || dto.QuantiteExportee <= 0)
                return BadRequest(new { message = "Taille requise et quantité exportée > 0." });

            var commande = await _context.CommandesClients.AnyAsync(c => c.Id == commandeId);
            if (!commande)
                return NotFound(new { message = "Commande introuvable." });

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
        [RequireModulePermission("commandes", requireWrite: true)]
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