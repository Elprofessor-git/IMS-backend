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
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<IEnumerable<MatelasGlobalDto>>> GetAll()
        {
            // Tous les matelas, y compris les partagés/historiques sans commande
            // (CommandeId NULL) : NumeroCommande vide dans ce cas.
            var matelas = await _context.Matelas
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

        [HttpPost]
        [RequireModulePermission("commandes,coupe")]
        public async Task<ActionResult<Matelas>> Create(CreateMatelasDto dto)
        {
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
    }
}