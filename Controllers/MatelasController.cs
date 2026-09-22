using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module « Coupe » transversal : vue globale de tous les matelas (toutes commandes),
    /// avec accès direct au rapport de coupe de leur commande via le frontend.
    /// Lecture — permission « coupe » (module dédié, distinct de « commandes »).
    /// Création de matelas partagée avec le suivi fournitures (module « commandes »).
    /// Garde-fous : unicité du numéro de matelas PAR COMMANDE (CommandeId + numéro,
    /// insensible casse/espaces — même règle que FournitureCommandeController.CreerMatelas) ;
    /// PUT/DELETE refusés (409) dès qu'un matelas porte des coupes enregistrées
    /// (l'historique ne se réécrit pas).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MatelasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MatelasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Unicité (CommandeId, NumeroMatelas) insensible casse/espaces : comparaison
        // normalisée (minuscules sans blancs) sur les matelas de la MÊME commande.
        // L'index unique physique correspondant est porté par la migration
        // 20260922090000_MatelasUniciteParCommande (fonctionnel UPPER+blancs retirés).
        private static string NormaliserNumeroMatelas(string n) =>
            string.Concat(n.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

        private async Task<bool> NumeroDejaPris(int? commandeId, string numero, int? matelasExclu = null)
        {
            var cle = NormaliserNumeroMatelas(numero);
            var numeros = await _context.Matelas
                .Where(m => m.CommandeId == commandeId && (matelasExclu == null || m.Id != matelasExclu.Value))
                .Select(m => m.NumeroMatelas)
                .ToListAsync();
            return numeros.Any(n => NormaliserNumeroMatelas(n) == cle);
        }

        [HttpGet]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<IEnumerable<MatelasGlobalDto>>> GetAll()
        {
            // Tous les matelas, y compris les partagés/historiques sans commande
            // (CommandeId NULL) : NumeroCommande vide dans ce cas.
            var matelas = await _context.Matelas
                .OrderBy(m => m.DateMatelas)
                .ThenBy(m => m.NumeroMatelas)
                .Select(m => new MatelasGlobalDto
                {
                    Id = m.Id,
                    CommandeId = m.CommandeId,
                    NumeroCommande = m.Commande != null ? m.Commande.NumeroCommande : string.Empty,
                    NumeroMatelas = m.NumeroMatelas,
                    DateMatelas = m.DateMatelas,
                    PiecePliage = m.PiecePliage,
                    CoupeEstimee = m.CoupeEstimee,
                    NombreCoupes = m.LotCoupes.Sum(lc => (int?)lc.QuantiteCoupee) ?? 0,
                    TotalPiecesCommandees = m.Commande != null
                        ? m.Commande.ConfigTailles.Sum(ct => (int?)ct.Quantite) ?? 0
                        : 0,
                    Notes = m.Notes,
                    EstActif = m.EstActif,
                })
                .ToListAsync();

            // Ordre de coupe = rang dans la séquence (DateMatelas, NumeroMatelas) croissant.
            // Vue calculée, aucune table de planning dédiée.
            for (var i = 0; i < matelas.Count; i++)
                matelas[i].OrdreDeCoupe = i + 1;

            return Ok(matelas.OrderByDescending(m => m.DateMatelas));
        }

        // Totaux globaux du module Coupe (toutes commandes confondues).
        [HttpGet("Stats")]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<MatelasStatsDto>> GetStats()
        {
            var piecesCommandees = await _context.CommandesClients
                .SelectMany(c => c.ConfigTailles)
                .SumAsync(ct => (int?)ct.Quantite) ?? 0;

            var dto = new MatelasStatsDto
            {
                TotalMatelas = await _context.Matelas.CountAsync(),
                TotalMatelasActifs = await _context.Matelas.CountAsync(m => m.EstActif),
                TotalPiecesCommandees = piecesCommandees,
                TotalPiecesCoupees = await _context.LotCoupes.SumAsync(l => (int?)l.QuantiteCoupee) ?? 0,
                TotalPiecesExportees = await _context.LotExports.SumAsync(l => (int?)l.QuantiteExportee) ?? 0,
            };

            return Ok(dto);
        }

        [HttpPost]
        [RequireModulePermission("commandes,coupe", requireWrite: true)]
        public async Task<ActionResult<Matelas>> Create(CreateMatelasDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NumeroMatelas))
                return BadRequest(new { message = "Le numéro du matelas est requis." });

            var commandeExiste = await _context.CommandesClients.AnyAsync(c => c.Id == dto.CommandeId);
            if (!commandeExiste)
                return NotFound(new { message = "Commande introuvable." });

            // Unicité par commande (insensible casse/espaces) — règle partagée avec
            // FournitureCommandeController.CreerMatelas. Deux commandes différentes
            // peuvent porter chacune un matelas « M1 ».
            if (await NumeroDejaPris(dto.CommandeId, dto.NumeroMatelas))
                return Conflict(new { message = $"Un matelas numéro '{dto.NumeroMatelas}' existe déjà pour cette commande." });

            var matelas = new Matelas
            {
                CommandeId = dto.CommandeId,
                NumeroMatelas = dto.NumeroMatelas,
                DateMatelas = dto.DateMatelas ?? DateTime.Now,
                PiecePliage = dto.PiecePliage,
                CoupeEstimee = dto.CoupeEstimee,
                Notes = dto.Notes,
            };

            _context.Matelas.Add(matelas);
            await _context.SaveChangesAsync();

            return Created($"/api/Matelas/{matelas.Id}", matelas);
        }

        [HttpPut("{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateMatelasDto dto)
        {
            var matelas = await _context.Matelas.FindAsync(id);
            if (matelas == null)
                return NotFound(new { message = "Matelas introuvable." });

            // Garde-fou : un matelas qui a déjà servi à des coupes est verrouillé.
            if (await _context.LotCoupes.AnyAsync(lc => lc.MatelasId == id))
                return Conflict(new { message = "Impossible de modifier un matelas qui a des coupes enregistrées." });

            var nouveauNumero = dto.NumeroMatelas ?? matelas.NumeroMatelas;
            if (string.IsNullOrWhiteSpace(nouveauNumero))
                return BadRequest(new { message = "Le numéro du matelas est requis." });

            // Unicité par commande (règle partagée) — hors matelas lui-même,
            // sur la commande cible (après un éventuel changement de commande).
            var commandeCible = dto.CommandeId ?? matelas.CommandeId;
            if (await NumeroDejaPris(commandeCible, nouveauNumero, matelas.Id))
                return Conflict(new { message = $"Un matelas numéro '{nouveauNumero}' existe déjà pour cette commande." });

            if (dto.CommandeId.HasValue && dto.CommandeId.Value != matelas.CommandeId)
            {
                if (!await _context.CommandesClients.AnyAsync(c => c.Id == dto.CommandeId.Value))
                    return NotFound(new { message = "Commande introuvable." });
                matelas.CommandeId = dto.CommandeId.Value;
            }

            if (dto.NumeroMatelas != null) matelas.NumeroMatelas = nouveauNumero;
            if (dto.DateMatelas.HasValue) matelas.DateMatelas = dto.DateMatelas.Value;
            if (dto.PiecePliage.HasValue) matelas.PiecePliage = dto.PiecePliage.Value;
            if (dto.CoupeEstimee.HasValue) matelas.CoupeEstimee = dto.CoupeEstimee.Value;
            if (dto.Notes != null) matelas.Notes = dto.Notes;
            if (dto.EstActif.HasValue) matelas.EstActif = dto.EstActif.Value;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Matelas mis à jour", id = matelas.Id });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("coupe", requireWrite: true)]
        public async Task<ActionResult> Delete(int id)
        {
            var matelas = await _context.Matelas.FindAsync(id);
            if (matelas == null)
                return NotFound(new { message = "Matelas introuvable." });

            // Garde-fou : pas de suppression tant que le matelas porte des coupes —
            // l'historique des coupes n'est jamais réécrit (conservation SetNull sinon).
            if (await _context.LotCoupes.AnyAsync(lc => lc.MatelasId == id))
                return Conflict(new { message = "Impossible de supprimer un matelas qui a des coupes enregistrées." });

            _context.Matelas.Remove(matelas);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Matelas supprimé" });
        }
    }
}