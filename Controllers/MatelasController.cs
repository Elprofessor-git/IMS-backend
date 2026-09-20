using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos.Commande;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module « Coupe » transversal : vue globale de tous les matelas (toutes commandes),
    /// avec accès direct au rapport de coupe de leur commande via le frontend.
    /// Lecture seule — permission « commandes » (réutilisation, comme le rapport de coupe).
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

        [HttpGet]
        [RequireModulePermission("commandes", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<MatelasGlobalDto>>> GetAll()
        {
            // CommandeId > 0 : on exclut les éventuels matelas legacy orphelins
            // (avant l'ajout de la FK, lignes sans commande — CommandeId NULL/0).
            var matelas = await _context.Matelas
                .Where(m => m.CommandeId > 0)
                .OrderByDescending(m => m.DateMatelas)
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

            return Ok(matelas);
        }
    }
}