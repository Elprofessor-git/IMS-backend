using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Dtos.Qualite;
using Backend_Gestion_Magasin_API.Models;
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

        private sealed record TripletKey(int CommandeId, int? ChaineProductionId, string Taille);

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

        // ═══════════ Dashboard Qualité (LOT 14) ═══════════
        // Point d'entrée du module : part de TOUTES les commandes actives
        // (Annulee et Terminee exclues) au lieu des seuls triplets ayant un export.
        // Une commande sans export reste visible via une ligne de synthèse
        // (EstCommandeSansExport = true) afin qu'une commande neuve soit
        // exploitable en moins de 3 clics. Ne modifie aucun calcul métier LOT 8 :
        // les soldes par triplet sont lus via QualiteService.CalculerSoldeAsync.
        [HttpGet("Dashboard")]
        [RequireModulePermission("qualite", requireWrite: false)]
        public async Task<ActionResult<QualiteDashboardDto>> Dashboard(
            [FromQuery] string? recherche,
            [FromQuery] string? statut)
        {
            var commandesActives = await _context.CommandesClients
                .Where(c => c.Statut != StatutCommande.Annulee && c.Statut != StatutCommande.Terminee)
                .Select(c => new
                {
                    c.Id,
                    c.NumeroCommande,
                    c.TitreCommande,
                    ClientNom = c.Client != null ? c.Client.Nom : null,
                    c.Statut,
                    c.DateCommande,
                })
                .ToListAsync();

            var idsCommandes = commandesActives.Select(c => c.Id).ToList();

            // Triplets (commande, chaîne, taille) réellement exportés.
            var triplets = await _context.LotExports
                .Where(le => idsCommandes.Contains(le.CommandeId))
                .GroupBy(le => new { le.CommandeId, le.ChaineProductionId, le.Taille })
                .Select(g => new
                {
                    g.Key.CommandeId,
                    g.Key.ChaineProductionId,
                    g.Key.Taille,
                    QuantiteExportee = g.Sum(x => x.QuantiteExportee),
                })
                .ToListAsync();

            var chaines = await _context.ChainesProduction
                .Select(cp => new { cp.Id, cp.Nom, cp.EstSousTraitant })
                .ToDictionaryAsync(cp => cp.Id);

            // Premier OF de chaque commande
            var ofParCommande = (await _context.OrdresFabrication
                    .Where(of => idsCommandes.Contains(of.CommandeId))
                    .OrderBy(of => of.Id)
                    .ToListAsync())
                .GroupBy(of => of.CommandeId)
                .ToDictionary(g => g.Key, g => g.First());

            // --- BATCH : calculer tous les soldes en UNE passe (remplace N appels CalculerSoldeAsync) ---
            var tripletObjects = triplets.Select(t => new QualiteService.Triplet(t.CommandeId, t.ChaineProductionId, t.Taille)).ToList();
            var soldes = await _qualite.CalculerSoldesAsync(tripletObjects);

            // BATCH : nombre de contrôles et d'envois par triplet
            var commandeIdsDansTriplets = triplets.Select(t => t.CommandeId).Distinct().ToList();
            var chaineIdsDansTriplets = triplets.Where(t => t.ChaineProductionId.HasValue).Select(t => t.ChaineProductionId!.Value).Distinct().ToList();
            var taillesDansTriplets = triplets.Select(t => t.Taille).Distinct().ToList();

            var controlesParTriplet = await _context.ControlesQualite
                .Where(c => c.OrdreFabrication != null
                         && commandeIdsDansTriplets.Contains(c.OrdreFabrication.CommandeId)
                         && (c.ChaineProductionId == null || chaineIdsDansTriplets.Contains(c.ChaineProductionId.Value))
                         && taillesDansTriplets.Contains(c.Taille))
                .GroupBy(c => new TripletKey(c.OrdreFabrication!.CommandeId, c.ChaineProductionId, c.Taille))
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var envoisParTriplet = await _context.EnvoisRetouche
                .Where(e => e.ControleSource != null
                         && commandeIdsDansTriplets.Contains(e.ControleSource.OrdreFabrication!.CommandeId)
                         && (e.ControleSource.ChaineProductionId == null || chaineIdsDansTriplets.Contains(e.ControleSource.ChaineProductionId.Value))
                         && taillesDansTriplets.Contains(e.ControleSource.Taille))
                .GroupBy(e => new TripletKey(e.ControleSource!.OrdreFabrication!.CommandeId, e.ControleSource.ChaineProductionId, e.ControleSource.Taille))
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var lignes = new List<QualiteDashboardLigneDto>();

            foreach (var t in triplets)
            {
                var cmd = commandesActives.First(c => c.Id == t.CommandeId);
                var tripletKey = new QualiteService.Triplet(t.CommandeId, t.ChaineProductionId, t.Taille);
                var solde = soldes.TryGetValue(tripletKey, out var s) ? s : new QualiteService.SoldeTriplet();

                bool estSousTraitant = false;
                string? chaineNom = null;
                if (t.ChaineProductionId.HasValue
                    && chaines.TryGetValue(t.ChaineProductionId.Value, out var cp))
                {
                    estSousTraitant = cp.EstSousTraitant;
                    chaineNom = cp.Nom;
                }

                var tripletKeyForCounts = new TripletKey(t.CommandeId, t.ChaineProductionId, t.Taille);
                var nbControles = controlesParTriplet.TryGetValue(tripletKeyForCounts, out var nc) ? nc : 0;
                var nbEnvois = envoisParTriplet.TryGetValue(tripletKeyForCounts, out var ne) ? ne : 0;

                var ligne = new QualiteDashboardLigneDto
                {
                    CommandeId = t.CommandeId,
                    NumeroCommande = cmd.NumeroCommande,
                    TitreCommande = cmd.TitreCommande,
                    ClientNom = cmd.ClientNom,
                    StatutCommande = cmd.Statut.ToString(),
                    DateCommande = cmd.DateCommande,
                    OrdreFabricationId = ofParCommande.TryGetValue(t.CommandeId, out var of) ? of.Id : (int?)null,
                    NumeroOF = ofParCommande.TryGetValue(t.CommandeId, out var of2) ? of2.NumeroOF : null,
                    ChaineProductionId = t.ChaineProductionId,
                    ChaineNom = chaineNom,
                    Taille = t.Taille,
                    EstCommandeSansExport = false,
                    QuantiteExportee = t.QuantiteExportee,
                    QuantiteControleeTotale = solde.QuantiteControleeTotale,
                    QuantiteAccepteeTotale = solde.QuantiteAccepteeTotale,
                    QuantiteRetoucheTotale = solde.QuantiteRetoucheTotale,
                    QuantiteRebutTotale = solde.QuantiteRebutTotale,
                    R1Restant = solde.R1Restant,
                    RRestant = solde.RRestant,
                    ERRestant = solde.ERRestant,
                    EnCours = solde.EnCours,
                    EstSolde = solde.EstSolde,
                    NombreControles = nbControles,
                    NombreEnvois = nbEnvois,
                };

                // Statut : mêmes règles dérivées que le board, extended à "pas d'export".
                if (ligne.QuantiteExportee == 0)
                {
                    ligne.Statut = "termine";
                    ligne.StatutLabel = "Terminé";
                }
                else if (ligne.EnCours == 0)
                {
                    ligne.Statut = "termine";
                    ligne.StatutLabel = "Terminé";
                }
                else if (ligne.ERRestant > 0)
                {
                    ligne.Statut = "en-retouche-sous-traitant";
                    ligne.StatutLabel = "En retouche chez le sous-traitant";
                }
                else if (ligne.RRestant > 0)
                {
                    ligne.Statut = estSousTraitant ? "retouches-a-renvoyer" : "retouche-sur-place";
                    ligne.StatutLabel = estSousTraitant ? "Retouches à renvoyer au sous-traitant" : "Retouche sur place";
                }
                else
                {
                    ligne.Statut = "en-attente-premier-controle";
                    ligne.StatutLabel = "En attente de premier contrôle";
                }

                if (statut != null && !string.Equals(statut, ligne.Statut, StringComparison.OrdinalIgnoreCase))
                    continue;

                lignes.Add(ligne);
            }

            // ── Synthèse des commandes actives qui n'ont aucun export (commande neuve) ──
            var commandesAvecTriplet = triplets.Select(t => t.CommandeId).ToHashSet();
            foreach (var cmd in commandesActives.Where(c => !commandesAvecTriplet.Contains(c.Id)))
            {
                var ligne = new QualiteDashboardLigneDto
                {
                    CommandeId = cmd.Id,
                    NumeroCommande = cmd.NumeroCommande,
                    TitreCommande = cmd.TitreCommande,
                    ClientNom = cmd.ClientNom,
                    StatutCommande = cmd.Statut.ToString(),
                    DateCommande = cmd.DateCommande,
                    OrdreFabricationId = ofParCommande.TryGetValue(cmd.Id, out var of3) ? of3.Id : (int?)null,
                    NumeroOF = ofParCommande.TryGetValue(cmd.Id, out var of4) ? of4.NumeroOF : null,
                    ChaineProductionId = null,
                    ChaineNom = null,
                    Taille = string.Empty,
                    EstCommandeSansExport = true,
                    QuantiteExportee = 0,
                    NombreControles = 0,
                    NombreEnvois = 0,
                    EstSolde = false,
                    EnCours = 0,
                    Statut = "aucun-export",
                    StatutLabel = ofParCommande.ContainsKey(cmd.Id)
                        ? "Aucun export — OF à alimenter"
                        : "Aucun export — OF à créer",
                };

                if (statut != null && !string.Equals(statut, ligne.Statut, StringComparison.OrdinalIgnoreCase))
                    continue;

                lignes.Add(ligne);
            }

            if (!string.IsNullOrWhiteSpace(recherche))
            {
                var q = recherche.Trim().ToLowerInvariant();
                lignes = lignes.Where(l =>
                    l.NumeroCommande.ToLowerInvariant().Contains(q)
                    || (l.TitreCommande ?? "").ToLowerInvariant().Contains(q)
                    || (l.ClientNom ?? "").ToLowerInvariant().Contains(q)
                    || (l.ChaineNom ?? "").ToLowerInvariant().Contains(q)
                    || l.Taille.ToLowerInvariant().Contains(q)
                ).ToList();
            }

            // Agrégation par commande : une commande est « avec contrôle » si AU MOINS
            // une de ses lignes en a, et « sans contrôle » UNIQUEMENT si aucune n'en a.
            // Sans ce regroupement, une commande à plusieurs triplets (dont un contrôlé et
            // un non contrôlé) était comptée dans les deux KPI à la fois.
            var parCommande = lignes
                .GroupBy(l => l.CommandeId)
                .Select(g => new
                {
                    AvecControle = g.Any(l => l.NombreControles > 0),
                    Soldee = g.All(l => l.EstSolde)
                })
                .ToList();

            var dto = new QualiteDashboardDto
            {
                Date = DateTime.Now,
                NombreCommandesActives = commandesActives.Count,
                NombreLignes = lignes.Count,
                QuantiteExporteeTotale = lignes.Sum(l => l.QuantiteExportee),
                QuantiteControleeTotale = lignes.Sum(l => l.QuantiteControleeTotale),
                QuantiteAccepteeTotale = lignes.Sum(l => l.QuantiteAccepteeTotale),
                QuantiteRetoucheTotale = lignes.Sum(l => l.QuantiteRetoucheTotale),
                QuantiteRebutTotale = lignes.Sum(l => l.QuantiteRebutTotale),
                CommandesAvecControle = parCommande.Count(c => c.AvecControle),
                CommandesSansControle = parCommande.Count(c => !c.AvecControle),
                CommandesSoldees = parCommande.Count(c => c.Soldee),
                Lignes = lignes
                    .OrderByDescending(l => l.EnCours)
                    .ThenBy(l => l.DateCommande)
                    .ThenBy(l => l.NumeroCommande)
                    .ToList(),
            };

            return Ok(dto);
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
                    ChaineNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,  // already correct
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

        // ═══════════ Journal du jour : contrôles + envois retouche aujourd'hui ═══════════

        [HttpGet("Journal")]
        [RequireModulePermission("qualite")]
        public async Task<ActionResult<IEnumerable<QualiteJournalLigneDto>>> GetJournal()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Contrôles aujourd'hui
            var controles = await _context.ControlesQualite
                .Where(c => c.DateControle >= today && c.DateControle < DateTime.Today.AddDays(1))
                .Select(c => new QualiteJournalLigneDto
                {
                    Id = c.Id,
                    Type = "controle",
                    NumeroCommande = c.OrdreFabrication != null ? c.OrdreFabrication.Commande.NumeroCommande : null,
                    Taille = c.Taille,
                    ChaineNom = c.ChaineProduction != null ? c.ChaineProduction.Nom : null,
                    QuantiteControlee = c.QuantiteControlee,
                    QuantiteAcceptee = c.QuantiteAcceptee,
                    QuantiteRetouche = c.QuantiteRetouche,
                    QuantiteRebut = c.QuantiteRebut,
                    TypeControle = c.TypeControle.ToString(),
                    EffectuePar = c.EffectuePar,
                    DateOperation = c.DateControle,
                    Notes = c.Notes,
                })
                .ToListAsync();

            // Envois retouche aujourd'hui
            var envois = await _context.EnvoisRetouche
                .Where(e => e.DateEnvoi >= today && e.DateEnvoi < DateTime.Today.AddDays(1))
                .Select(e => new QualiteJournalLigneDto
                {
                    Id = e.Id,
                    Type = "envoi",
                    NumeroCommande = e.ControleSource != null && e.ControleSource.OrdreFabrication != null
                        ? e.ControleSource.OrdreFabrication.Commande.NumeroCommande
                        : null,
                    Taille = e.ControleSource != null ? e.ControleSource.Taille : string.Empty,
                    ChaineNom = e.ChaineProduction != null ? e.ChaineProduction.Nom : null,  // already correct
                    QuantiteRenvoyee = e.QuantiteRenvoyee,
                    EffectuePar = e.EffectuePar,
                    DateOperation = e.DateEnvoi,
                    Notes = e.Notes,
                })
                .ToListAsync();

            var journal = controles.Cast<QualiteJournalLigneDto>()
                .Concat(envois.Cast<QualiteJournalLigneDto>())
                .OrderByDescending(j => j.DateOperation)
                .ToList();

            return Ok(journal);
        }

    }
}
