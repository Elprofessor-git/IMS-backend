using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Coupe
{
    /// <summary>
    /// MOTEUR UNIQUE (C3). Le rapport de coupe (onglet consultation) et l'ordre de
    /// coupe document (L2) sont deux VUES du même calcul : une seule passe de
    /// <see cref="MoteurCoupe"/> sur les gabarits de la commande et les plans des
    /// matelas, puis les cumuls par taille (plan théorique, coupes réelles, exports,
    /// seuil de marge) sont dérivés de ce résultat commun. Il n'existe donc aucune
    /// zone de calcul indépendante qui pourrait diverger.
    ///
    /// Les DTO d'API restent construits PAR les contrôleurs à partir du même résultat :
    /// les réponses existantes (historique) gardent exactement leur forme.
    /// </summary>
    public sealed class RapportCoupeService
    {
        private readonly ApplicationDbContext _context;

        public RapportCoupeService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Cumuls par taille et par matelas de toute la commande. Renvoie null si la
        /// commande n'existe pas (les contrôleurs gèrent le 404).
        /// </summary>
        public async Task<CalculCoupeCommande?> CalculerAsync(int commandeId)
        {
            var commande = await _context.CommandesClients
                .Include(c => c.ConfigTailles)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == commandeId);
            if (commande == null)
                return null;

            // Matelas de la commande, dans l'ordre de l'atelier (DateMatelas, NumeroMatelas).
            var matelas = await _context.Matelas
                .AsNoTracking()
                .Where(m => m.CommandeId == commandeId)
                .Include(m => m.PlanDeCoupeLignes)
                .Include(m => m.LotCoupes)
                .ToListAsync();

            var matelasTries = matelas
                .OrderBy(m => m.DateMatelas)
                .ThenBy(m => m.NumeroMatelas)
                .ToList();

            // La règle des restes vit au seul endroit du système : le moteur partagé.
            // Chaque matelas est une passe de l'échelle.
            var echelle = MoteurCoupe.Calculer(
                commande.ConfigTailles
                    .OrderBy(c => c.Id)
                    .Select(c => new Gabarit(c.Taille, c.Quantite))
                    .ToList(),
                matelasTries.Select((m, i) => new PasseCoupe(
                    i + 1,
                    m.PiecePliage,
                    m.PlanDeCoupeLignes.ToDictionary(
                        p => p.Taille,
                        p => p.Occurrences,
                        StringComparer.OrdinalIgnoreCase),
                    m.NumeroMatelas)).ToList());

            // Coupes réelles de la commande entière (avec ou sans matelas rattaché).
            var lots = await _context.LotCoupes.AsNoTracking()
                .Where(l => l.CommandeId == commandeId)
                .ToListAsync();
            var exports = await _context.LotExports.AsNoTracking()
                .Where(l => l.CommandeId == commandeId)
                .ToListAsync();

            var coupeParTaille = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sansMatelasParTaille = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var exportParTaille = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var lot in lots)
            {
                coupeParTaille[lot.Taille] = coupeParTaille.GetValueOrDefault(lot.Taille) + lot.QuantiteCoupee;
                if (lot.MatelasId == null)
                    sansMatelasParTaille[lot.Taille] = sansMatelasParTaille.GetValueOrDefault(lot.Taille) + lot.QuantiteCoupee;
            }
            foreach (var e in exports)
                exportParTaille[e.Taille] = exportParTaille.GetValueOrDefault(e.Taille) + e.QuantiteExportee;

            // Plan théorique par taille = contribution de chaque passe du moteur.
            var planParTaille = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var passe in echelle.Passes)
                foreach (var (taille, contribution) in passe.Contributions)
                    planParTaille[taille] = planParTaille.GetValueOrDefault(taille) + contribution;

            var seuilMarge = (decimal)commande.MargeSecuriteDefaut;
            var tailles = new List<CumulTaille>(commande.ConfigTailles.Count);
            foreach (var ct in commande.ConfigTailles.OrderBy(c => c.Taille))
            {
                var plan = planParTaille.GetValueOrDefault(ct.Taille);
                var coupe = coupeParTaille.GetValueOrDefault(ct.Taille);
                var sans = sansMatelasParTaille.GetValueOrDefault(ct.Taille);
                var exp = exportParTaille.GetValueOrDefault(ct.Taille);
                tailles.Add(new CumulTaille(
                    ct.Taille,
                    ct.Quantite,
                    ct.Quantite * (1m + seuilMarge / 100m),
                    plan,
                    coupe,
                    sans,
                    exp));
            }

            var matelasCalcules = new List<MatelasCalcule>(matelasTries.Count);
            for (var i = 0; i < matelasTries.Count; i++)
            {
                var m = matelasTries[i];
                var passe = echelle.Passes[i];
                var lignes = new List<LignePlanCalcule>(m.PlanDeCoupeLignes.Count);

                foreach (var ligne in m.PlanDeCoupeLignes.OrderBy(p => p.Taille))
                {
                    var theorique = passe.Contributions.GetValueOrDefault(ligne.Taille);
                    var coupeReelle = m.LotCoupes
                        .Where(lc => string.Equals(lc.Taille, ligne.Taille, StringComparison.OrdinalIgnoreCase))
                        .Sum(lc => (int?)lc.QuantiteCoupee) ?? 0;
                    lignes.Add(new LignePlanCalcule(
                        ligne.Id,
                        m.Id,
                        ligne.Taille,
                        ligne.Occurrences,
                        theorique,
                        coupeReelle,
                        theorique - coupeReelle));
                }

                var totalCoupeReelle = m.LotCoupes.Sum(lc => (int?)lc.QuantiteCoupee) ?? 0;
                matelasCalcules.Add(new MatelasCalcule(
                    m.Id,
                    m.NumeroMatelas,
                    m.DateMatelas,
                    m.PiecePliage,
                    m.Longueur,
                    m.Laize,
                    i + 1,
                    lignes.Sum(l => l.Theorique),
                    totalCoupeReelle,
                    lignes.Sum(l => l.ResteACouper),
                    lignes));
            }

            return new CalculCoupeCommande
            {
                MargeSecuriteDefaut = seuilMarge,
                TotalQuantiteCommande = tailles.Sum(t => t.QuantiteCommande),
                TotalPlanTheorique = tailles.Sum(t => t.PlanTheorique),
                TotalCoupeReelleMatelas = matelasCalcules.Sum(m => m.TotalCoupeReelle),
                TotalCoupesSansMatelas = tailles.Sum(t => t.CoupesSansMatelas),
                TotalExportee = tailles.Sum(t => t.Exportee),
                Surplus = echelle.Surplus,
                Manque = echelle.Manque,
                Tailles = tailles,
                Matelas = matelasCalcules,
            };
        }
    }

    /// <summary>Ligne taille → quantités (commandée, seuil de marge, plan théorique, coupes, exports).</summary>
    public sealed record CumulTaille(
        string Taille,
        int QuantiteCommande,
        decimal Seuil,
        int PlanTheorique,
        int CoupeReelle,
        int CoupesSansMatelas,
        int Exportee)
    {
        /// <summary>Dépassement toléré jusqu'à la marge de sécurité par défaut.</summary>
        public bool DepassePlan => PlanTheorique > Seuil;
        public bool DepasseCoupe => CoupeReelle > Seuil;
        public bool DepasseExport => Exportee > CoupeReelle;
    }

    /// <summary>Ligne de plan d'un matelas : théorique = contributions du moteur, coupes réelles du matelas.</summary>
    public sealed record LignePlanCalcule(
        int LigneId,
        int MatelasId,
        string Taille,
        int Occurrences,
        int Theorique,
        int CoupeReelle,
        int ResteACouper);

    /// <summary>Cumul par matelas : plis, théorique, coupes réelles, reste.</summary>
    public sealed record MatelasCalcule(
        int MatelasId,
        string NumeroMatelas,
        DateTime DateMatelas,
        int PiecePliage,
        decimal? Longueur,
        decimal? Laize,
        int OrdreDeCoupe,
        int TotalTheorique,
        int TotalCoupeReelle,
        int ResteTotal,
        IReadOnlyList<LignePlanCalcule> Lignes);

    /// <summary>Résultat du moteur unique, partagé par les vues « rapport » et « ordre de coupe ».</summary>
    public sealed class CalculCoupeCommande
    {
        public decimal MargeSecuriteDefaut { get; init; }
        public int TotalQuantiteCommande { get; init; }
        public int TotalPlanTheorique { get; init; }
        /// <summary>Coupes réelles rattachées à un matelas (ordre de coupe document).</summary>
        public int TotalCoupeReelleMatelas { get; init; }
        public int TotalCoupesSansMatelas { get; init; }
        public int TotalExportee { get; init; }
        public long Surplus { get; init; }
        public long Manque { get; init; }
        public IReadOnlyList<CumulTaille> Tailles { get; init; } = new List<CumulTaille>();
        public IReadOnlyList<MatelasCalcule> Matelas { get; init; } = new List<MatelasCalcule>();

        /// <summary>Toutes les coupes réelles de la commande (avec ou sans matelas rattaché).</summary>
        public int TotalCoupesToutes => TotalCoupeReelleMatelas + TotalCoupesSansMatelas;
    }
}