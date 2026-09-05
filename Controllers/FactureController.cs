using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Facture;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FactureController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ExcelExportService _excel;

        public FactureController(ApplicationDbContext context, ExcelExportService excel)
        {
            _context = context;
            _excel = excel;
        }

        [HttpGet]
        [RequireModulePermission("factures", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<FactureListDto>>> GetFactures()
        {
            return await _context.Factures
                .OrderByDescending(f => f.DateFacture)
                .Select(f => new FactureListDto
                {
                    Id = f.Id,
                    NumeroFacture = f.NumeroFacture,
                    DateFacture = f.DateFacture,
                    ClientId = f.ClientId,
                    ClientNom = f.Client.Nom,
                    Devise = f.Devise,
                    MontantTotal = f.Lignes.Sum(l => l.MontantLigne),
                    Statut = f.Statut,
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireModulePermission("factures", requireWrite: false)]
        public async Task<ActionResult<FactureDetailDto>> GetFacture(int id)
        {
            var dto = await GetFactureDetailAsync(id);
            if (dto == null)
                return NotFound(new { message = "Facture introuvable." });

            return Ok(dto);
        }

        // Export Excel : remplit le gabarit « FAC 3318.xlsx ».
        [HttpGet("{id}/Export")]
        [RequireModulePermission("factures", requireWrite: false)]
        public async Task<IActionResult> ExportFacture(int id)
        {
            var dto = await GetFactureDetailAsync(id);
            if (dto == null)
                return NotFound(new { message = "Facture introuvable." });

            var bytes = _excel.ExportFacture(dto);
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Facture_{dto.NumeroFacture}.xlsx");
        }

        private async Task<FactureDetailDto?> GetFactureDetailAsync(int id)
        {
            return await _context.Factures
                .Where(f => f.Id == id)
                .Select(f => new FactureDetailDto
                {
                    Id = f.Id,
                    NumeroFacture = f.NumeroFacture,
                    DateFacture = f.DateFacture,
                    ClientId = f.ClientId,
                    ClientNom = f.Client.Nom,
                    ClientAdresse = f.Client.Adresse,
                    Devise = f.Devise,
                    ModePaiement = f.ModePaiement,
                    Rib = f.Rib,
                    Iban = f.Iban,
                    ModeLivraison = f.ModeLivraison,
                    NombreColis = f.NombreColis,
                    PoidsNetKg = f.PoidsNetKg,
                    PoidsBrutKg = f.PoidsBrutKg,
                    VolumeM3 = f.VolumeM3,
                    Notes = f.Notes,
                    Statut = f.Statut,
                    DateCreation = f.DateCreation,
                    CreePar = f.CreePar,
                    MontantTotal = f.Lignes.Sum(l => l.MontantLigne),
                    Lignes = f.Lignes.Select(l => new FactureCommandeLigneDto
                    {
                        Id = l.Id,
                        FactureId = l.FactureId,
                        CommandeId = l.CommandeId,
                        Modele = l.Modele,
                        NumeroCommande = l.Commande.NumeroCommande,
                        Quantite = l.Quantite,
                        PrixUnitaireFacon = l.PrixUnitaireFacon,
                        MontantLigne = l.MontantLigne,
                    }).ToList()
                })
                .FirstOrDefaultAsync();
        }

        [HttpPost]
        [RequireModulePermission("factures", requireWrite: true)]
        public async Task<ActionResult<Models.Facture>> PostFacture(CreateFactureDto dto)
        {
            if (dto.ClientId <= 0)
                return BadRequest(new { message = "Client requis." });
            if (dto.Lignes.Count == 0)
                return BadRequest(new { message = "Au moins une ligne de commande requise." });

            var client = await _context.Clients.FindAsync(dto.ClientId);
            if (client == null)
                return NotFound(new { message = "Client introuvable." });

            var commandes = await _context.CommandesClients
                .Where(c => dto.Lignes.Select(l => l.CommandeId).Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var facture = new Models.Facture
            {
                NumeroFacture = GenerateNumeroFacture(),
                DateFacture = dto.DateFacture ?? DateTime.Now,
                ClientId = dto.ClientId,
                Devise = dto.Devise ?? "EUR",
                ModePaiement = dto.ModePaiement,
                Rib = dto.Rib,
                Iban = dto.Iban,
                ModeLivraison = dto.ModeLivraison,
                NombreColis = dto.NombreColis,
                PoidsNetKg = dto.PoidsNetKg,
                PoidsBrutKg = dto.PoidsBrutKg,
                VolumeM3 = dto.VolumeM3,
                Notes = dto.Notes,
                Statut = StatutFacture.Brouillon,
                DateCreation = DateTime.Now,
                CreePar = User.Identity?.Name,
            };

            foreach (var l in dto.Lignes)
            {
                if (l.Quantite <= 0 || l.PrixUnitaireFacon < 0)
                    return BadRequest(new { message = "Quantité > 0 et prix façon ≥ 0 requis sur chaque ligne." });
                if (!commandes.TryGetValue(l.CommandeId, out var commande))
                    return BadRequest(new { message = $"Commande {l.CommandeId} introuvable." });

                facture.Lignes.Add(new FactureCommandeLigne
                {
                    CommandeId = l.CommandeId,
                    Modele = commande.TitreCommande ?? commande.NumeroCommande,
                    Quantite = l.Quantite,
                    PrixUnitaireFacon = l.PrixUnitaireFacon,
                    MontantLigne = l.Quantite * l.PrixUnitaireFacon,
                });
            }

            _context.Factures.Add(facture);

            const int maxTentatives = 5;
            for (var tentative = 1; ; tentative++)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (EstConflitNumeroFacture(ex) && tentative < maxTentatives)
                {
                    facture.NumeroFacture = GenerateNumeroFacture();
                }
            }

            return CreatedAtAction("GetFacture", new { id = facture.Id }, facture);
        }

        [HttpPut("{id}")]
        [RequireModulePermission("factures", requireWrite: true)]
        public async Task<IActionResult> PutFacture(int id, UpdateFactureDto dto)
        {
            var facture = await _context.Factures
                .Include(f => f.Lignes)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (facture == null)
                return NotFound(new { message = "Facture introuvable." });

            if (facture.Statut == StatutFacture.Emise || facture.Statut == StatutFacture.Payee)
                return BadRequest(new { message = "Une facture émise ou payée ne peut plus être modifiée." });

            var commandes = await _context.CommandesClients
                .Where(c => dto.Lignes.Select(l => l.CommandeId).Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            facture.DateFacture = dto.DateFacture;
            facture.Devise = dto.Devise ?? "EUR";
            facture.ModePaiement = dto.ModePaiement;
            facture.Rib = dto.Rib;
            facture.Iban = dto.Iban;
            facture.ModeLivraison = dto.ModeLivraison;
            facture.NombreColis = dto.NombreColis;
            facture.PoidsNetKg = dto.PoidsNetKg;
            facture.PoidsBrutKg = dto.PoidsBrutKg;
            facture.VolumeM3 = dto.VolumeM3;
            facture.Notes = dto.Notes;

            _context.FactureCommandesLignes.RemoveRange(facture.Lignes);
            facture.Lignes.Clear();

            foreach (var l in dto.Lignes)
            {
                if (l.Quantite <= 0 || l.PrixUnitaireFacon < 0)
                    return BadRequest(new { message = "Quantité > 0 et prix façon ≥ 0 requis sur chaque ligne." });
                if (!commandes.TryGetValue(l.CommandeId, out var commande))
                    return BadRequest(new { message = $"Commande {l.CommandeId} introuvable." });

                facture.Lignes.Add(new FactureCommandeLigne
                {
                    CommandeId = l.CommandeId,
                    Modele = commande.TitreCommande ?? commande.NumeroCommande,
                    Quantite = l.Quantite,
                    PrixUnitaireFacon = l.PrixUnitaireFacon,
                    MontantLigne = l.Quantite * l.PrixUnitaireFacon,
                });
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/Emettre")]
        [RequireModulePermission("factures", requireWrite: true)]
        public async Task<IActionResult> EmettreFacture(int id)
        {
            var facture = await _context.Factures.FindAsync(id);
            if (facture == null)
                return NotFound(new { message = "Facture introuvable." });
            if (facture.Statut == StatutFacture.Annulee)
                return BadRequest(new { message = "Impossible d'émettre une facture annulée." });

            facture.Statut = StatutFacture.Emise;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Facture émise" });
        }

        [HttpPost("{id}/Regler")]
        [RequireModulePermission("factures", requireWrite: true)]
        public async Task<IActionResult> ReglerFacture(int id)
        {
            var facture = await _context.Factures.FindAsync(id);
            if (facture == null)
                return NotFound(new { message = "Facture introuvable." });
            if (facture.Statut != StatutFacture.Emise)
                return BadRequest(new { message = "Seule une facture émise peut être réglée." });

            facture.Statut = StatutFacture.Payee;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Facture réglée" });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("factures", requireWrite: true)]
        public async Task<IActionResult> DeleteFacture(int id)
        {
            var facture = await _context.Factures.FindAsync(id);
            if (facture == null)
                return NotFound(new { message = "Facture introuvable." });

            if (facture.Statut == StatutFacture.Payee)
                return BadRequest(new { message = "Impossible de supprimer une facture réglée." });

            _context.Factures.Remove(facture);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private string GenerateNumeroFacture()
        {
            var annee = DateTime.Now.Year;
            var max = _context.Factures
                .Where(f => f.NumeroFacture.StartsWith($"FAC{annee}"))
                .Select(f => f.NumeroFacture)
                .AsEnumerable()
                .Select(n => int.TryParse(n.Replace($"FAC{annee}", ""), out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"FAC{annee}{max + 1:D4}";
        }

        private static bool EstConflitNumeroFacture(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException pg && pg.SqlState == "23505";
        }
    }
}