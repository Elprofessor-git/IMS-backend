using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Dtos.Qualite;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Cycle retouche qualité — règles §4.4 de la conception v1.1.
    /// Triplet comptable : (CommandeId, ChaineProductionId, Taille).
    ///   R1( t)    = Q − Σ contrôlées tour 1            (tour 1)
    ///   RN+1(ER)  = QuantiteRenvoyee(ER) − Σ re-contrôlées(fils(ER))  (tours ≥ 2)
    ///   Plafond envoi (STRICT, sans ForcerDepassement) : Σ envois(C) ≤ QuantiteRetouche(C)
    ///   Identité : Q = ΣA + ΣB + EnCours(t) ; clôture triplet quand EnCours = 0.
    /// Couche additive : aucune des méthodes protégées (ValiderRessources,
    /// Calculer, LivrerAchat, RecevoirImportation, RecevoirPartiel) n'est touchée.
    /// </summary>
    public class QualiteService
    {
        private readonly ApplicationDbContext _context;

        public QualiteService(ApplicationDbContext context)
        {
            _context = context;
        }

        public record Triplet(int CommandeId, int? ChaineId, string Taille);

        /// <summary>Q = Σ QuantiteExportee des LotExport du triplet.</summary>
        public async Task<int> QuantiteExporteeAsync(Triplet t)
        {
            return await _context.LotExports
                .Where(le => le.CommandeId == t.CommandeId
                          && le.ChaineProductionId == t.ChaineId
                          && le.Taille == t.Taille)
                .SumAsync(le => (int?)le.QuantiteExportee) ?? 0;
        }

        /// <summary>Contrôles tour 1 du triplet (TypeControle = Interne, sans parent).</summary>
        private IQueryable<ControleQualite> Tour1Query(Triplet t) =>
            _context.ControlesQualite.Where(c =>
                c.OrdreFabrication != null
                && c.OrdreFabrication.CommandeId == t.CommandeId
                && c.ChaineProductionId == t.ChaineId
                && c.Taille == t.Taille
                && c.TypeControle == TypeControle.Interne
                && c.ControleParentId == null);

        /// <summary>Tous les contrôles du triplet (tous tours).</summary>
        private IQueryable<ControleQualite> ControlsOfTripletQuery(Triplet t) =>
            _context.ControlesQualite.Where(c =>
                c.OrdreFabrication != null
                && c.OrdreFabrication.CommandeId == t.CommandeId
                && c.ChaineProductionId == t.ChaineId
                && c.Taille == t.Taille);

        /// <summary>Référence de saisie restante sur le triplet, selon le tour demandé.</summary>
        /// <returns>null si l'entrée (tour) n'a pas de sens.</returns>
        public async Task<int?> ReferenceDisponibleAsync(
            int commandeId, int? chaineId, string taille,
            string typeControle, int? controleParentId, int? envoiRetoucheId)
        {
            var triplet = new Triplet(commandeId, chaineId, taille);
            var isInterne = string.Equals(typeControle, nameof(TypeControle.Interne), StringComparison.OrdinalIgnoreCase);
            var isRetour = string.Equals(typeControle, nameof(TypeControle.RetourSousTraitant), StringComparison.OrdinalIgnoreCase);

            // Tour 2+ : re-contrôle exclusivement nourri par un EnvoiRetouche (4.4).
            if (isRetour || envoiRetoucheId.HasValue)
            {
                if (!envoiRetoucheId.HasValue)
                    return null;
                var envoi = await _context.EnvoisRetouche
                    .FirstOrDefaultAsync(e => e.Id == envoiRetoucheId.Value);
                if (envoi == null)
                    return null;
                // Garde héritée 4.4 : l'envoi renvoie vers SA chaîne source
                // (garde applicative appliquée à la création, re-vérifiée ici).
                var parentOfEnvoi = await _context.ControlesQualite
                    .FirstOrDefaultAsync(c => c.Id == envoi.ControleQualiteId);
                if (parentOfEnvoi != null && envoi.ChaineProductionId != parentOfEnvoi.ChaineProductionId)
                    return null;

                var dejaRecontrole = await _context.ControlesQualite
                    .Where(c => c.EnvoiRetoucheId == envoi.Id)
                    .SumAsync(c => (int?)c.QuantiteControlee) ?? 0;
                return Math.Max(0, envoi.QuantiteRenvoyee - dejaRecontrole);
            }

            // Tour 1 : RN1 = Q − déjà contrôlé en tour 1.
            if (!isInterne)
                return null;
            var q = await QuantiteExporteeAsync(triplet);
            var dejaControle = await Tour1Query(triplet).SumAsync(c => (int?)c.QuantiteControlee) ?? 0;
            return Math.Max(0, q - dejaControle);
        }

        /// <summary>
        /// EnCours(t) — §4.4, 3 termes :
        ///   R1_restant = Q − Σ contrôlées tour 1
        ///   + Σ_C ( R(C) − Σ envois(C) )
        ///   + Σ_ER ( QuantiteRenvoyee(ER) − Σ QuantiteControlee(fils(ER)) )
        /// </summary>
        public async Task<SoldeTriplet> CalculerSoldeAsync(Triplet t)
        {
            var q = await QuantiteExporteeAsync(t);

            var controlCounts = await ControlsOfTripletQuery(t)
                .Select(c => new
                {
                    c.Id,
                    c.QuantiteControlee,
                    c.QuantiteRetouche,
                    Type = c.TypeControle,
                    c.ControleParentId,
                })
                .ToListAsync();

            var envois = await _context.EnvoisRetouche
                .Where(e => controlCounts.Select(c => c.Id).Contains(e.ControleQualiteId))
                .Select(e => new
                {
                    e.Id,
                    ControleQualiteId = e.ControleQualiteId,
                    QuantiteRenvoyee = (int?)e.QuantiteRenvoyee ?? 0,
                })
                .ToListAsync();

            var recontrolledParEnvoi = await _context.ControlesQualite
                .Where(c => c.EnvoiRetoucheId != null && envois.Select(e => e.Id).Contains(c.EnvoiRetoucheId!.Value))
                .GroupBy(c => c.EnvoiRetoucheId!.Value)
                .Select(g => new { EnvoiId = g.Key, Recontrole = g.Sum(c => (int?)c.QuantiteControlee) ?? 0 })
                .ToListAsync();

            var tour1Controlee = controlCounts
                .Where(c => c.Type == TypeControle.Interne && c.ControleParentId == null)
                .Sum(c => (int?)c.QuantiteControlee) ?? 0;

            var r1Restant = Math.Max(0, q - tour1Controlee);

            var rRestant = 0;
            foreach (var c in controlCounts)
            {
                var envoyes = envois.Where(e => e.ControleQualiteId == c.Id).Sum(e => e.QuantiteRenvoyee);
                rRestant += Math.Max(0, c.QuantiteRetouche - envoyes);
            }

            var erRestant = 0;
            foreach (var e2 in envois)
            {
                var e2Id = e2.Id;
                var recontrole = recontrolledParEnvoi.FirstOrDefault(r => r.EnvoiId == e2Id)?.Recontrole ?? 0;
                erRestant += Math.Max(0, e2.QuantiteRenvoyee - recontrole);
            }

            var enCours = r1Restant + rRestant + erRestant;

            var sommeAAcceptee = await ControlsOfTripletQuery(t).SumAsync(c => (int?)c.QuantiteAcceptee) ?? 0;
            var sommeBRebut = await ControlsOfTripletQuery(t).SumAsync(c => (int?)c.QuantiteRebut) ?? 0;

            return new SoldeTriplet
            {
                QuantiteExportee = q,
                QuantiteControleeTotale = await ControlsOfTripletQuery(t).SumAsync(c => (int?)c.QuantiteControlee) ?? 0,
                QuantiteAccepteeTotale = sommeAAcceptee,
                QuantiteRetoucheTotale = await ControlsOfTripletQuery(t).SumAsync(c => (int?)c.QuantiteRetouche) ?? 0,
                QuantiteRebutTotale = sommeBRebut,
                R1Restant = r1Restant,
                RRestant = rRestant,
                ERRestant = erRestant,
                EnCours = enCours,
                EstSolde = enCours == 0 && q > 0,
            };
        }

        public record SoldeTriplet
        {
            public int QuantiteExportee { get; init; }
            public int QuantiteControleeTotale { get; init; }
            public int QuantiteAccepteeTotale { get; init; }
            public int QuantiteRetoucheTotale { get; init; }
            public int QuantiteRebutTotale { get; init; }
            public int R1Restant { get; init; }
            public int RRestant { get; init; }
            public int ERRestant { get; init; }
            public int EnCours { get; init; }
            public bool EstSolde { get; init; }
        }

        /// <summary>
        /// Clôture de la commande quand l'égalité comptable Q = ΣA + ΣB est
        /// vérifiée pour TOUS les triplets (CommandeId, Chaîne, Taille) de la
        /// commande. Remplace l'ancienne clôture automatique portée par
        /// TacheProduction (module Magasin) — découplage PARTIE A.
        /// </summary>
        public async Task<bool> CloturerCommandeSiSoldee(int commandeId)
        {
            var triplets = await _context.LotExports
                .Where(le => le.CommandeId == commandeId)
                .Select(le => new { le.ChaineProductionId, le.Taille })
                .Distinct()
                .ToListAsync();

            foreach (var t3 in triplets)
            {
                var solde = await CalculerSoldeAsync(new Triplet(commandeId, t3.ChaineProductionId, t3.Taille));
                if (!solde.EstSolde)
                    return false;
            }

            var commande = await _context.CommandesClients.FindAsync(commandeId);
            if (commande == null)
                return false;

            if (commande.Statut != StatutCommande.Terminee && commande.Statut != StatutCommande.Annulee)
            {
                commande.Statut = StatutCommande.Terminee;
                commande.DateMiseAJour = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return true;
        }
    }
}