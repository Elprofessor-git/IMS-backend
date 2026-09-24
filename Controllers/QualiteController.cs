using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Dtos.Qualite;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Module Qualité — board Kanban, fil du cycle, indicateurs (conception §4.5-4.6).
    /// Statuts dérivés du cycle (jamais saisis) :
    ///   en-attente-premier-controle / retouches-a-renvoyer / retouche-sur-place /
    ///   en-retouche-sous-traitant / termine
    /// Permissions : module « qualite ».
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QualiteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly QualiteService _qualite;

        public QualiteController(ApplicationDbContext context, QualiteService quality)
        {
            _context = context;
            _qualite = quality;
        }

        public class BoardCard
        {
            public int CommandeId { get; set; }
            public string? NumeroCommande { get; set; }
            public int? ChaineProductionId { get; set; }
            public string? ChaineNom { get; set; }
            public bool ChaineEstSousTraitant { get; set; }
            public string Taille { get; set; } = string.Empty;
            public int QuantiteExportee { get; set; }
            public int QuantiteControleeTotale { get; set; }
            public int QuantiteAccepteeTotale { get; set; }
            public int QuantiteRetoucheTotale { get; set; }
            public int QuantiteRebutTotale { get; set; }
            public int R1Restant { get; set; }
            public int RRestant { get; set; }
            public int ERRestant { get; set; }
            public int EnCours { get; set; }
            public bool EstSolde { get; set; }
            public string Statut { get; set; } = string.Empty;
            public string StatutLabel { get; set; } = string.Empty;
            public int NombreControles { get; set; }
            public int NombreEnvois { get; set; }
        }

        [HttpGet("Board")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<BoardCard>>> Board(
            [FromQuery] int? commandeId,
            [FromQuery] int? ofId,
            [FromQuery] int? chaineId,
            [FromQuery] string? taille,
            [FromQuery] string? statut)
        {
            int? targetCommandeId = commandeId;
            if (ofId.HasValue)
            {
                var of = await _context.OrdresFabrication.FindAsync(ofId.Value);
                if (of == null)
                    return NotFound(new { message = "Ordre de fabrication introuvable." });
                targetCommandeId = of.CommandeId;
            }

            var triplets = await _context.LotExports
                .Where(le =>
                    (targetCommandeId == null || le.CommandeId == targetCommandeId)
                    && (chaineId == null || le.ChaineProductionId == chaineId)
                    && (taille == null || le.Taille == taille))
                .GroupBy(le => new { le.CommandeId, le.ChaineProductionId, le.Taille })
                .Select(g => new BoardCard
                {
                    CommandeId = g.Key.CommandeId,
                    ChaineProductionId = g.Key.ChaineProductionId,
                    Taille = g.Key.Taille,
                    QuantiteExportee = g.Sum(x => x.QuantiteExportee),
                })
                .ToListAsync();

            var commandes = await _context.CommandesClients
                .Where(c => c.Statut != Backend_Gestion_Magasin_API.Models.StatutCommande.Annulee)
                .Select(c => new { c.Id, c.NumeroCommande })
                .ToListAsync();

            var cards = new List<BoardCard>();
            foreach (var t in triplets)
            {
                var solde = await _qualite.CalculerSoldeAsync(new QualiteService.Triplet(t.CommandeId, t.ChaineProductionId, t.Taille));
                t.NumeroCommande = commandes.FirstOrDefault(c => c.Id == t.CommandeId)?.NumeroCommande;
                t.QuantiteControleeTotale = solde.QuantiteControleeTotale;
                t.QuantiteAccepteeTotale = solde.QuantiteAccepteeTotale;
                t.QuantiteRetoucheTotale = solde.QuantiteRetoucheTotale;
                t.QuantiteRebutTotale = solde.QuantiteRebutTotale;
                t.R1Restant = solde.R1Restant;
                t.RRestant = solde.RRestant;
                t.ERRestant = solde.ERRestant;
                t.EnCours = solde.EnCours;
                t.EstSolde = solde.EstSolde;

                // Séquences d'envois / contrôles
                var isSousTraitant = t.ChaineProductionId.HasValue
                    && await _context.ChainesProduction.Where(cp => cp.Id == t.ChaineProductionId.Value)
                        .Select(cp => cp.EstSousTraitant).FirstOrDefaultAsync();
                if (!t.ChaineProductionId.HasValue)
                    isSousTraitant = false;
                t.ChaineEstSousTraitant = isSousTraitant;
                t.ChaineNom = t.ChaineProductionId.HasValue
                    ? await _context.ChainesProduction.Where(cp => cp.Id == t.ChaineProductionId.Value)
                        .Select(cp => cp.Nom).FirstOrDefaultAsync()
                    : null;

                var controls = await _context.ControlesQualite
                    .Where(c => c.OrdreFabrication != null
                             && c.OrdreFabrication.CommandeId == t.CommandeId
                             && c.ChaineProductionId == t.ChaineProductionId
                             && c.Taille == t.Taille)
                    .Select(c => new { c.Id, c.TypeControle })
                    .ToListAsync();
                var envois = await _context.EnvoisRetouche
                    .Where(e => controls.Select(x => x.Id).Contains(e.ControleQualiteId))
                    .CountAsync();

                t.NombreControles = controls.Count;
                t.NombreEnvois = envois;

                // ── Statut dérivé (4.5) ──
                (t.Statut, t.StatutLabel) = DeriverStatut(t);
                if (statut != null && !string.Equals(statut, t.Statut, StringComparison.OrdinalIgnoreCase))
                    continue;

                cards.Add(t);
            }

            // Ordre du board : en-cours d'abord, terminés en fin.
            return Ok(cards
                .OrderByDescending(c => c.EnCours)
                .ThenBy(c => c.NumeroCommande));
        }

        private static (string, string) DeriverStatut(BoardCard c)
        {
            if (c.QuantiteExportee == 0 || c.EnCours == 0)
                return ("termine", "Terminé");
            if (c.ERRestant > 0)
                return ("en-retouche-sous-traitant", "En retouche chez le sous-traitant");
            if (c.RRestant > 0)
                return c.ChaineEstSousTraitant
                    ? ("retouches-a-renvoyer", "Retouches à renvoyer au sous-traitant")
                    : ("retouche-sur-place", "Retouche sur place");
            if (c.R1Restant > 0)
                return ("en-attente-premier-controle", "En attente de premier contrôle");
            return ("termine", "Terminé");
        }

        // ═══════════ Fil du cycle d'un triplet (4.5 — dialog Réception + Contrôle) ═══════════

        [HttpGet("Cycle")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<CycleQualiteDto>> Cycle(
            [FromQuery] int? commandeId,
            [FromQuery] int? ofId,
            [FromQuery] int? chaineId,
            [FromQuery] string? taille)
        {
            int? targetCommandeId = commandeId;
            if (ofId.HasValue)
            {
                var of = await _context.OrdresFabrication.FindAsync(ofId.Value);
                if (of == null)
                    return NotFound(new { message = "Ordre de fabrication introuvable." });
                targetCommandeId = of.CommandeId;
            }

            if (targetCommandeId == null || string.IsNullOrWhiteSpace(taille))
                return BadRequest(new { message = "Commande (ou OF) et taille sont requis pour afficher le cycle." });

            var triplet = new QualiteService.Triplet(targetCommandeId.Value, chaineId, taille);
            var solde = await _qualite.CalculerSoldeAsync(triplet);

            var cycle = new CycleQualiteDto
            {
                OrdreFabricationId = ofId,
                NumeroCommande = await _context.CommandesClients
                    .Where(cc => cc.Id == targetCommandeId.Value).Select(cc => cc.NumeroCommande).FirstOrDefaultAsync(),
                ChaineProductionId = chaineId,
                ChaineNom = chaineId.HasValue
                    ? await _context.ChainesProduction.Where(cp => cp.Id == chaineId.Value).Select(cp => cp.Nom).FirstOrDefaultAsync()
                    : null,
                Taille = taille,
                QuantiteExportee = solde.QuantiteExportee,
                QuantiteControleeTotale = solde.QuantiteControleeTotale,
                QuantiteAccepteeTotale = solde.QuantiteAccepteeTotale,
                QuantiteRetoucheTotale = solde.QuantiteRetoucheTotale,
                QuantiteRebutTotale = solde.QuantiteRebutTotale,
                EnCours = solde.EnCours,
                EstSolde = solde.EstSolde,
            };

            cycle.Controles = await _context.ControlesQualite
                .Where(c => c.OrdreFabrication != null
                         && c.OrdreFabrication.CommandeId == targetCommandeId.Value
                         && c.ChaineProductionId == chaineId
                         && c.Taille == taille)
                .OrderBy(c => c.DateControle)
                .Select(c => new ControleQualiteDto
                {
                    Id = c.Id,
                    OrdreFabricationId = c.OrdreFabricationId,
                    NumeroCommande = c.OrdreFabrication != null ? c.OrdreFabrication.Commande.NumeroCommande : null,
                    ChaineProductionId = c.ChaineProductionId,
                    ChaineNom = c.ChaineProduction != null ? c.ChaineProduction.Nom : null,
                    TypeControle = c.TypeControle.ToString(),
                    ControleParentId = c.ControleParentId,
                    EnvoiRetoucheId = c.EnvoiRetoucheId,
                    Taille = c.Taille,
                    QuantiteControlee = c.QuantiteControlee,
                    QuantiteAcceptee = c.QuantiteAcceptee,
                    QuantiteRetouche = c.QuantiteRetouche,
                    QuantiteRebut = c.QuantiteRebut,
                    DateControle = c.DateControle,
                    EffectuePar = c.EffectuePar,
                    Notes = c.Notes,
                })
                .ToListAsync();

            cycle.Envois = await _context.EnvoisRetouche
                .Where(e => cycle.Controles.Select(x => x.Id).Contains(e.ControleQualiteId))
                .OrderBy(e => e.DateEnvoi)
                .Select(e => new EnvoiRetoucheDto
                {
                    Id = e.Id,
                    ControleQualiteId = e.ControleQualiteId,
                    ChaineProductionId = e.ChaineProductionId,
                    ChaineNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,
                    QuantiteRenvoyee = e.QuantiteRenvoyee,
                    DateEnvoi = e.DateEnvoi,
                    EffectuePar = e.EffectuePar,
                    Notes = e.Notes,
                })
                .ToListAsync();

            return Ok(cycle);
        }

        // ═══════════ Indicateurs (4.6 — vues calculées, aucune table) ═══════════

        public class IndicateursQualiteDto
        {
            public int NombreControles { get; set; }
            public decimal TauxRebut { get; set; }
            public decimal TauxRetouche { get; set; }
            public decimal TauxAcceptation { get; set; }
            public decimal TauxReRetouche { get; set; }
            public decimal DelaiMoyenRetourRetoucheJours { get; set; }
            public int ToursMoyensParTriplet { get; set; }
            public List<TopDefautDto> TopDefauts { get; set; } = new();
            public List<SoldeTripletDto> Soldes { get; set; } = new();
        }

        public class TopDefautDto
        {
            public string Code { get; set; } = string.Empty;
            public string Libelle { get; set; } = string.Empty;
            public int Quantite { get; set; }
        }

        public class SoldeTripletDto
        {
            public string NumeroCommande { get; set; } = string.Empty;
            public string? ChaineNom { get; set; }
            public string Taille { get; set; } = string.Empty;
            public int QuantiteExportee { get; set; }
            public int EnCours { get; set; }
            public bool EstSolde { get; set; }
        }

        [HttpGet("Indicateurs")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<IndicateursQualiteDto>> Indicateurs(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int? commandeId,
            [FromQuery] int? chaineId)
        {
            var controlsQuery = _context.ControlesQualite.AsQueryable();
            if (dateDebut.HasValue)
                controlsQuery = controlsQuery.Where(c => c.DateControle >= dateDebut.Value);
            if (dateFin.HasValue)
                controlsQuery = controlsQuery.Where(c => c.DateControle <= dateFin.Value);
            if (commandeId.HasValue)
                controlsQuery = controlsQuery.Where(c => c.OrdreFabrication != null && c.OrdreFabrication.CommandeId == commandeId.Value);
            if (chaineId.HasValue)
                controlsQuery = controlsQuery.Where(c => c.ChaineProductionId == chaineId.Value);

            var controles = await controlsQuery
                .Select(c => new
                {
                    c.Id,
                    c.TypeControle,
                    c.QuantiteControlee,
                    c.QuantiteAcceptee,
                    c.QuantiteRetouche,
                    c.QuantiteRebut,
                    c.DateControle,
                    c.OrdreFabricationId,
                })
                .ToListAsync();

            var indicateurs = new IndicateursQualiteDto
            {
                NombreControles = controles.Count,
            };

            var totalControlee = controles.Sum(c => (int?)c.QuantiteControlee) ?? 0;
            var totalAcceptee = controles.Sum(c => (int?)c.QuantiteAcceptee) ?? 0;
            var totalRetouche = controles.Sum(c => (int?)c.QuantiteRetouche) ?? 0;
            var totalRebut = controles.Sum(c => (int?)c.QuantiteRebut) ?? 0;

            indicateurs.TauxAcceptation = totalControlee > 0 ? Math.Round((decimal)totalAcceptee * 100 / totalControlee, 2) : 0;
            indicateurs.TauxRetouche = totalControlee > 0 ? Math.Round((decimal)totalRetouche * 100 / totalControlee, 2) : 0;
            indicateurs.TauxRebut = totalControlee > 0 ? Math.Round((decimal)totalRebut * 100 / totalControlee, 2) : 0;

            // Taux de re-retouche = Σ Retouche(tours ≥ 2) / Σ QuantiteRenvoyee(tours ≥ 2)
            var toursSup = controles.Where(c => c.TypeControle == Backend_Gestion_Magasin_API.Models.TypeControle.RetourSousTraitant).ToList();
            var retoucheToursSup = toursSup.Sum(c => (int?)c.QuantiteRetouche) ?? 0;

            // Envois tours ≥ 2 : envois re-contrôlés par un contrôle RetourSousTraitant.
            var envoisToursSup = await _context.EnvoisRetouche
                .Where(e => _context.ControlesQualite.Any(c => c.EnvoiRetoucheId == e.Id && c.TypeControle == Backend_Gestion_Magasin_API.Models.TypeControle.RetourSousTraitant))
                .SumAsync(e => (int?)e.QuantiteRenvoyee) ?? 0;

            indicateurs.TauxReRetouche = envoisToursSup > 0 ? Math.Round((decimal)retoucheToursSup * 100 / envoisToursSup, 2) : 0;

            // Délai moyen retour de retouche = Moyenne(DateControle re-contrôle − DateEnvoi EnvoiRetouche)
            var delais = await _context.ControlesQualite
                .Where(c => c.EnvoiRetouche != null)
                .Select(c => new { EnvoiDate = c.EnvoiRetouche!.DateEnvoi, ControleDate = c.DateControle })
                .ToListAsync();
            if (delais.Any())
                indicateurs.DelaiMoyenRetourRetoucheJours = Math.Round((decimal)delais.Average(d => (d.ControleDate - d.EnvoiDate).TotalDays), 2);

            // Tours moyens par triplet = moyenne du nombre de contrôles par triplet (OF, chaîne, taille)
            var groups = controles
                .GroupBy(c => new { c.OrdreFabricationId })
                .ToList();
            indicateurs.ToursMoyensParTriplet = groups.Count > 0 ? (int)Math.Round(groups.Average(g => (double)g.Count())) : 0;

            // Top 5 codes défauts
            indicateurs.TopDefauts = await _context.ControleQualiteDefautLignes
                .Where(l => controles.Select(c => c.Id).Contains(l.ControleQualiteId))
                .GroupBy(l => new { l.DefautCode.Code, l.DefautCode.Libelle })
                .OrderByDescending(g => g.Sum(x => x.Quantite))
                .Take(5)
                .Select(g => new TopDefautDto
                {
                    Code = g.Key.Code,
                    Libelle = g.Key.Libelle,
                    Quantite = g.Sum(x => x.Quantite),
                })
                .ToListAsync();

            // Solde par triplet (égalité comptable 4.4) — indicateur de cohérence.
            var exportTriplets = await _context.LotExports
                .Where(le => (commandeId == null || le.CommandeId == commandeId)
                          && (chaineId == null || le.ChaineProductionId == chaineId))
                .GroupBy(le => new { le.CommandeId, le.ChaineProductionId, le.Taille })
                .Select(g => new { g.Key.CommandeId, g.Key.ChaineProductionId, g.Key.Taille, Q = g.Sum(x => x.QuantiteExportee) })
                .ToListAsync();

            var numeroParCommande = await _context.CommandesClients
                .Select(c => new { c.Id, c.NumeroCommande })
                .ToListAsync();
            var nomParChaine = await _context.ChainesProduction
                .Select(c => new { c.Id, c.Nom })
                .ToListAsync();

            foreach (var t in exportTriplets)
            {
                var solde = await _qualite.CalculerSoldeAsync(new QualiteService.Triplet(t.CommandeId, t.ChaineProductionId, t.Taille));
                indicateurs.Soldes.Add(new SoldeTripletDto
                {
                    NumeroCommande = numeroParCommande.FirstOrDefault(c => c.Id == t.CommandeId)?.NumeroCommande ?? t.CommandeId.ToString(),
                    ChaineNom = t.ChaineProductionId.HasValue ? nomParChaine.FirstOrDefault(c => c.Id == t.ChaineProductionId.Value)?.Nom : null,
                    Taille = t.Taille,
                    QuantiteExportee = solde.QuantiteExportee,
                    EnCours = solde.EnCours,
                    EstSolde = solde.EstSolde,
                });
            }

            return Ok(indicateurs);
        }
    }
}