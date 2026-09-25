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
    /// Module « Coupe » — agrégats de pilotage par COMMANDE (tableau de bord global).
    /// Lecture seule : aucune écriture, aucune migration. Tous les totaux sont
    /// calculés à la volée (agrégats SQL fusionnés en mémoire), rien n'est persisté.
    /// </summary>
    // Route explicite « api/Coupe » (le nom du contrôleur donnerait « api/CoupeDashboard »).
    [Route("api/Coupe")]
    [ApiController]
    [Authorize]
    public class CoupeDashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CoupeDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Avancement de la coupe par commande. Point de départ : les commandes
        /// (annulées exclues) — donc y compris celles qui n'ont AUCUN matelas, qui
        /// apparaissent avec planifié = 0 et reste à planifier = demande totale.
        /// Cohérent avec /api/Matelas/Stats : Σ PiecesDemandees = TotalPiecesCommandees
        /// sur la même population.
        /// </summary>
        [HttpGet("Dashboard")]
        [RequireModulePermission("coupe")]
        public async Task<ActionResult<CoupeDashboardDto>> GetDashboard()
        {
            // 1. Les commandes + leur demande (Σ ConfigTaille) — une seule requête.
            var commandes = await _context.CommandesClients
                .Where(c => c.Statut != StatutCommande.Annulee)
                .Select(c => new
                {
                    c.Id,
                    c.NumeroCommande,
                    c.TitreCommande,
                    c.DateCommande,
                    Statut = c.Statut.ToString(),
                    ClientNom = c.Client != null ? c.Client.Nom : null,
                    PlateformeNom = c.Client != null && c.Client.Plateforme != null
                        ? c.Client.Plateforme.Nom
                        : null,
                    Demande = c.ConfigTailles.Sum(ct => (int?)ct.Quantite) ?? 0,
                })
                .ToListAsync();

            var ids = commandes.Select(c => c.Id).ToList();

            // 2. Σ plans (Occurrences × PiecePliage) et nombre de matelas, par commande.
            var parMatelas = await _context.Matelas
                .Where(m => m.CommandeId != null && ids.Contains(m.CommandeId.Value))
                .GroupBy(m => m.CommandeId!.Value)
                .Select(g => new
                {
                    CommandeId = g.Key,
                    NombreMatelas = g.Count(),
                    Planifie = g.Sum(m => m.PlanDeCoupeLignes.Sum(p => (int?)(p.Occurrences * m.PiecePliage))) ?? 0,
                })
                .ToListAsync();

            // 3. Σ coupes par commande, et dont combien sont orphelines (sans matelas).
            var parCoupe = await _context.LotCoupes
                .Where(l => ids.Contains(l.CommandeId))
                .GroupBy(l => l.CommandeId)
                .Select(g => new
                {
                    CommandeId = g.Key,
                    Coupe = g.Sum(l => (int?)l.QuantiteCoupee) ?? 0,
                    SansMatelas = g.Count(l => l.MatelasId == null),
                })
                .ToListAsync();

            var planParCommande = parMatelas.ToDictionary(x => x.CommandeId, x => x);
            var coupeParCommande = parCoupe.ToDictionary(x => x.CommandeId, x => x);

            var lignes = new List<CoupeDashboardCommandeDto>(commandes.Count);
            foreach (var c in commandes)
            {
                var planifie = planParCommande.TryGetValue(c.Id, out var pm) ? pm.Planifie : 0;
                var coupe = coupeParCommande.TryGetValue(c.Id, out var pc) ? pc.Coupe : 0;

                lignes.Add(new CoupeDashboardCommandeDto
                {
                    CommandeId = c.Id,
                    NumeroCommande = c.NumeroCommande,
                    TitreCommande = c.TitreCommande,
                    ClientNom = c.ClientNom,
                    PlateformeNom = c.PlateformeNom,
                    Statut = c.Statut,
                    DateCommande = c.DateCommande,
                    NombreMatelas = planParCommande.TryGetValue(c.Id, out var pmm) ? pmm.NombreMatelas : 0,
                    PiecesDemandees = c.Demande,
                    PiecesPlanifiees = planifie,
                    PiecesCoupees = coupe,
                    // Bornes à 0 : une commande peut être planifiée ou coupée au-delà
                    // de sa demande (marge, dépassement forcé) — le « reste » n'est jamais négatif.
                    ResteAPlanifier = Math.Max(0, c.Demande - planifie),
                    ResteACouper = Math.Max(0, planifie - coupe),
                    CoupesSansMatelas = coupeParCommande.TryGetValue(c.Id, out var pcs) ? pcs.SansMatelas : 0,
                    Avancement = c.Demande > 0
                        ? (int)Math.Min(100, Math.Round(coupe * 100.0 / c.Demande))
                        : 0,
                });
            }

            // Le plus « en retard » d'abord : ce qu'il reste à faire, calcul ou coupe.
            var triees = lignes
                .OrderByDescending(l => l.ResteAPlanifier + l.ResteACouper)
                .ThenByDescending(l => l.DateCommande)
                .ToList();

            var totalPlanifie = triees.Sum(l => l.PiecesPlanifiees);
            var totalCoupe = triees.Sum(l => l.PiecesCoupees);

            return Ok(new CoupeDashboardDto
            {
                Date = DateTime.Now,
                NombreCommandes = triees.Count,
                PiecesDemandees = triees.Sum(l => l.PiecesDemandees),
                PiecesPlanifiees = totalPlanifie,
                PiecesCoupees = totalCoupe,
                // Sommes des restes par commande (et non recalcul sur les totaux) : un reste
                // borné à 0 par commande reste borné à 0 dans le total.
                ResteAPlanifier = triees.Sum(l => l.ResteAPlanifier),
                ResteACouper = triees.Sum(l => l.ResteACouper),
                Commandes = triees,
            });
        }
    }
}
