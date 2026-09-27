using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Dtos.Production;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module Production — agrégats de pilotage par COMMANDE (tableau de bord global).
    /// Lecture seule : aucune écriture, aucune migration. Tous les totaux sont
    /// calculés à la volée (agrégats SQL fusionnés en mémoire), rien n'est persisté.
    /// </summary>
    [Route("api/Production")]
    [ApiController]
    [Authorize]
    public class ProductionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductionController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tableau de bord Production : agrégats d'étapes par commande active.
        /// Commandes actives = Statut != Annulee && Statut != Terminee.
        /// </summary>
        [HttpGet("Dashboard")]
        [RequireModulePermission("production")]
        public async Task<ActionResult<ProductionDashboardDto>> GetDashboard()
        {
            // 1. Commandes actives + demande (config tailles) + client + plateforme
            var commandes = await _context.CommandesClients
                .Where(c => c.Statut != StatutCommande.Annulee && c.Statut != StatutCommande.Terminee)
                .Select(c => new CommandeInfo
                {
                    Id = c.Id,
                    NumeroCommande = c.NumeroCommande,
                    TitreCommande = c.TitreCommande,
                    DateCommande = c.DateCommande,
                    Statut = c.Statut.ToString(),
                    ClientNom = c.Client != null ? c.Client.Nom : null,
                    PlateformeNom = c.Client != null && c.Client.Plateforme != null ? c.Client.Plateforme.Nom : null,
                })
                .ToListAsync();

            var ids = commandes.Select(c => c.Id).ToList();

            // 2. Étapes par commande
            var etapesParCommande = await _context.OrdresFabricationEtapes
                .Where(e => e.OrdreFabrication != null && ids.Contains(e.OrdreFabrication.CommandeId))
                .Select(e => new EtapeInfo
                {
                    CommandeId = e.OrdreFabrication!.CommandeId,
                    TypeEtape = e.TypeEtape,
                    Statut = e.Statut,
                    ChaineProductionId = e.ChaineProductionId,
                    ChaineProductionNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    DateFinPrevue = e.DateFinPrevue,
                    DateFinReelle = e.DateFinReelle,
                    DateCreation = e.DateCreation,
                })
                .ToListAsync();

            var etapesGroupees = etapesParCommande
                .GroupBy(x => x.CommandeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3. Exports (LotExport) par commande + chaîne - materialize first to avoid EF Core expression tree issues
            var exportsRaw = await _context.LotExports
                .Where(le => ids.Contains(le.CommandeId))
                .GroupBy(le => new { le.CommandeId, le.ChaineProductionId })
                .Select(g => new
                {
                    CommandeId = g.Key.CommandeId,
                    ChaineProductionId = g.Key.ChaineProductionId,
                    TotalExports = g.Count(),
                    PiecesExportees = g.Sum(x => (int?)x.QuantiteExportee),
                })
                .ToListAsync();

            var exportsParCommande = exportsRaw
                .Select(x => new ExportInfo
                {
                    CommandeId = (int?)(x.CommandeId) ?? 0,
                    ChaineProductionId = (int?)(x.ChaineProductionId) ?? 0,
                    TotalExports = x.TotalExports,
                    PiecesExportees = x.PiecesExportees ?? 0,
                })
                .ToList();

            var exportsDict = exportsParCommande
                .GroupBy(x => x.CommandeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var lignes = new List<ProductionDashboardCommandeDto>(commandes.Count);
            foreach (var c in commandes)
            {
                var etapes = etapesGroupees.TryGetValue(c.Id, out var e) ? e : new List<EtapeInfo>();
                var nbEtapes = etapes.Count;
                var enCours = etapes.Count(x => x.Statut == StatutTache.EnCours);
                var bloquees = etapes.Count(x => x.Statut == StatutTache.Bloque);
                var terminees = etapes.Count(x => x.Statut == StatutTache.Termine);
                var aPlanifier = etapes.Count(x => x.Statut == StatutTache.NonCommence);

                // Étape courante : première non terminée (NonCommence ou EnCours ou Bloque), triée par DateFinPrevue puis DateCreation
                var etapesNonTerminees = etapes
                    .Where(x => x.Statut != StatutTache.Termine)
                    .OrderBy(x => x.DateFinPrevue ?? DateTime.MaxValue)
                    .ThenBy(x => x.DateCreation)
                    .ToList();

                var etapeCourante = etapesNonTerminees.FirstOrDefault();

                // Retard : au moins une étape non terminée avec DateFinPrevue < Now
                var enRetard = etapes.Any(x => x.Statut != StatutTache.Termine && x.DateFinPrevue.HasValue && x.DateFinPrevue.Value < DateTime.Now);

                // Chaîne principale : la plus fréquente parmi les étapes
                var chainePrincipale = etapes
                    .Where(x => x.ChaineProductionId.HasValue)
                    .GroupBy(x => x.ChaineProductionId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First().ChaineProductionNom)
                    .FirstOrDefault();

                // Avancement % = terminées / total * 100
                var avancement = nbEtapes > 0 ? (int)Math.Min(100, Math.Round((double)terminees * 100.0 / nbEtapes)) : 0;

                lignes.Add(new ProductionDashboardCommandeDto
                {
                    CommandeId = c.Id,
                    NumeroCommande = c.NumeroCommande,
                    TitreCommande = c.TitreCommande,
                    ClientNom = c.ClientNom,
                    Statut = c.Statut,
                    DateCommande = c.DateCommande,
                    NombreEtapes = nbEtapes,
                    EtapesEnCours = enCours,
                    EtapesBloquees = bloquees,
                    EtapesTerminees = terminees,
                    EtapesAPlanifier = aPlanifier,
                    EtapeCouranteType = etapeCourante?.TypeEtape.ToString(),
                    EtapeCouranteStatut = etapeCourante?.Statut.ToString(),
                    EtapeCouranteChaine = etapeCourante?.ChaineProductionNom,
                    EtapeCouranteDateFinPrevue = etapeCourante?.DateFinPrevue,
                    EnRetard = enRetard,
                    Avancement = avancement,
                    ChainePrincipaleNom = chainePrincipale,
                });
            }

            // Tri : le plus en retard d'abord, puis par date commande
            var triees = lignes
                .OrderByDescending(l => l.EnRetard)
                .ThenByDescending(l => l.EtapesEnCours + l.EtapesBloquees) // priorité aux commandes avec étapes actives
                .ThenByDescending(l => l.DateCommande)
                .ToList();

            var totalEtapes = triees.Sum(l => l.NombreEtapes);
            var totalEnCours = triees.Sum(l => l.EtapesEnCours);
            var totalBloquees = triees.Sum(l => l.EtapesBloquees);
            var totalTerminees = triees.Sum(l => l.EtapesTerminees);
            var totalEnRetard = triees.Count(l => l.EnRetard);

            return Ok(new ProductionDashboardDto
            {
                Date = DateTime.Now,
                NombreCommandes = triees.Count,
                TotalEtapes = totalEtapes,
                EtapesEnCours = totalEnCours,
                EtapesBloquees = totalBloquees,
                EtapesTerminees = totalTerminees,
                CommandesEnRetard = totalEnRetard,
                Commandes = triees,
            });
        }

        // Exports (LotExport) par commande + chaîne
        [HttpGet("Exports/{commandeId}")]
        [RequireModulePermission("production")]
        public async Task<ActionResult<IEnumerable<ProductionExportParChaineDto>>> GetExports(int commandeId)
        {
            var exports = await _context.LotExports
                .Where(le => le.CommandeId == commandeId)
                .GroupBy(le => new { le.ChaineProductionId, ChaineNom = le.ChaineProduction != null ? le.ChaineProduction.Nom : (string?)null })
                .Select(g => new ProductionExportParChaineDto
                {
                    ChaineProductionId = g.Key.ChaineProductionId ?? 0,
                    ChaineNom = g.Key.ChaineNom ?? "Sans chaîne",
                    TotalExports = g.Count(),
                    PiecesExportees = g.Sum(x => (int?)x.QuantiteExportee) ?? 0,
                })
                .ToListAsync();
            return Ok(exports);
        }

        // Journal du jour : transitions d'étapes
        [HttpGet("Journal")]
        [RequireModulePermission("production")]
        public async Task<ActionResult<IEnumerable<ProductionJournalLigneDto>>> GetJournal()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var journal = await _context.OrdresFabricationEtapes
                .Where(e => e.DateMiseAJour >= today && e.DateMiseAJour < tomorrow)
                .Select(e => new ProductionJournalLigneDto
                {
                    Id = e.Id,
                    EtapeId = e.Id,
                    NumeroOF = e.OrdreFabrication != null ? e.OrdreFabrication.NumeroOF : null,
                    CommandeId = e.OrdreFabrication != null ? e.OrdreFabrication.CommandeId : 0,
                    NumeroCommande = e.OrdreFabrication != null ? e.OrdreFabrication.Commande.NumeroCommande : null,
                    TypeEtape = e.TypeEtape.ToString(),
                    AncienStatut = null, // pas d'historique dans ce modèle
                    NouveauStatut = e.Statut.ToString(),
                    ChaineNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    DateTransition = e.DateMiseAJour ?? DateTime.MinValue,
                    EffectuePar = e.CreePar,
                })
                .OrderByDescending(j => j.DateTransition)
                .ToListAsync();

            return Ok(journal);
        }
    }

}
