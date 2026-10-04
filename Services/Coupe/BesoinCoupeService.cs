using System.Security.Cryptography;
using System.Text;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Services.Coupe;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services.Coupe
{
    /// <summary>
    /// Construit et relit le besoin de coupe au grain (commande, article, couleur).
    ///
    /// Choix structurant : le manque n'est PAS stocke. Il depend de la couverture
    /// reelle (stock, achats), qui bouge toute la journee. Le stocker produirait un
    /// chiffre perime presente comme vrai ; on le recalcule donc a la lecture, et on
    /// compare l'empreinte du calcul courant a celle memorisee pour signaler une
    /// ligne plutot que d'afficher un nombre faux.
    ///
    /// L'echelle de coupe elle-meme est conftee a <see cref="MoteurCoupe"/> : une
    /// seule implementation de la regle des restes, verifiee sur les 54 feuilles du
    /// classeur de reference.
    ///
    /// Portee volontairement restreinte : aucun travail d'API ni d'interface.
    /// </summary>
    public sealed class BesoinCoupeService
    {
        private readonly ApplicationDbContext _db;

        public BesoinCoupeService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Reconstruit les lignes de besoin de la commande depuis les besoins
        /// officiels (BesoinCommande), groupes au grain (commande, article, couleur),
        /// et remplace les lignes precedentes.
        ///
        /// BesoinCommande est la source : c'est deja la nomenclature officielle, et
        /// elle porte la couleur, que la BOM n'a pas.
        /// </summary>
        public async Task<ResultatSynchronisation> SynchroniserAsync(int commandeId, CancellationToken ct = default)
        {
            var besoins = await _db.BesoinsCommandes
                .AsNoTracking()
                .Where(b => b.CommandeClientId == commandeId)
                .ToListAsync(ct);

            if (besoins.Count == 0)
                return new ResultatSynchronisation(0, 0, "aucun besoin officiel sur la commande");

            var lignes = besoins
                .GroupBy(b => new { b.ArticleId, Couleur = Normaliser(b.Couleur) })
                .Select(g => new BesoinCoupe
                {
                    CommandeClientId = commandeId,
                    ArticleId = g.Key.ArticleId,
                    Couleur = g.Key.Couleur,
                    QuantiteCommandee = g.Sum(b => (long)b.QuantiteTotale) is var q && q > int.MaxValue
                        ? int.MaxValue
                        : (int)q,
                    MetrageAnnonce = 0m,
                    EmpreinteCalcul = string.Empty,
                })
                .ToList();

            foreach (var ligne in lignes)
                ligne.MetrageAnnonce = await MetrageAnnonceAsync(commandeId, ligne.ArticleId, ct);

            // Remplacement complet : le besoin est un etat derive, pas un historique.
            var anciennes = await _db.BesoinsCoupe
                .Where(b => b.CommandeClientId == commandeId)
                .ToListAsync(ct);
            _db.BesoinsCoupe.RemoveRange(anciennes);
            _db.BesoinsCoupe.AddRange(lignes);

            await _db.SaveChangesAsync(ct);

            return new ResultatSynchronisation(
                lignes.Count,
                lignes.Sum(l => (long)l.QuantiteCommandee),
                $"{lignes.Count} ligne(s) au grain (commande, article, couleur)");
        }

        /// <summary>
        /// Relit le besoin de coupe avec un manque CALCULE a la volee.
        ///
        /// La couverture est recalculee depuis le stock et les achats, jamais lue
        /// dans BesoinCommande.EstCompletementCouvert : ce drapeau est fige au
        /// dernier calcul BOM, donc il ne bouge pas avec les receptions.
        /// </summary>
        public async Task<IReadOnlyList<BesoinCoupeLigne>> RelireAsync(int commandeId, CancellationToken ct = default)
        {
            var stockees = await _db.BesoinsCoupe
                .AsNoTracking()
                .Where(b => b.CommandeClientId == commandeId)
                .OrderBy(b => b.ArticleId)
                .ThenBy(b => b.Couleur)
                .ToListAsync(ct);

            if (stockees.Count == 0) return [];

            var clientId = await _db.CommandesClients
                .AsNoTracking()
                .Where(c => c.Id == commandeId)
                .Select(c => (int?)c.ClientId)
                .FirstOrDefaultAsync(ct);

            var groupeIds = await _db.GroupeCommandeCommandes
                .AsNoTracking()
                .Where(g => g.CommandeClientId == commandeId)
                .Select(g => g.GroupeCommandeId)
                .ToListAsync(ct);

            var lignes = new List<BesoinCoupeLigne>(stockees.Count);

            foreach (var b in stockees)
            {
                var disponible = await CouvertureAsync(commandeId, b.ArticleId, clientId, groupeIds, ct);
                var quantite = b.QuantiteCommandee;
                var manque = Math.Max(0, quantite - disponible);
                var empreinte = CalculerEmpreinte(quantite, disponible, b.Couleur);

                lignes.Add(new BesoinCoupeLigne(
                    b.Id,
                    b.CommandeClientId,
                    b.ArticleId,
                    b.Couleur,
                    b.MetrageAnnonce,
                    quantite,
                    disponible,
                    manque,
                    manque > 0,
                    b.EmpreinteCalcul.Length > 0 && b.EmpreinteCalcul != empreinte));
            }

            return lignes;
        }

        /// <summary>
        /// Couverture recalculee d'un article pour la commande : tissu importe
        /// rattache a la commande, puis a son client, puis a la plateforme, puis au
        /// groupe.
        ///
        /// Meme perimetre que le calcul de couverture de CommandeClientController
        /// (memes quatre portees, meme filtre TypeStock.Importe), pour que les deux
        /// chiffres ne se contredisent pas. Le stock reserve et le stock libre ne
        /// comptent pas ici : ce sont des disponibilites, pas du tissu commande.
        /// </summary>
        private async Task<long> CouvertureAsync(
            int commandeId, int articleId, int? clientId, List<int> groupeIds, CancellationToken ct)
        {
            var stocks = _db.Stocks.AsNoTracking()
                .Where(s => s.ArticleId == articleId
                         && s.Quantite > 0
                         && s.TypeStock == TypeStock.Importe);

            long total = 0;

            total += await stocks
                .Where(s => s.CommandeClientId == commandeId)
                .SumAsync(s => (long)s.Quantite, ct);

            total += await stocks
                .Where(s => s.CommandeClientId == null
                         && s.ClientId != null && clientId != null && s.ClientId == clientId)
                .SumAsync(s => (long)s.Quantite, ct);

            if (clientId != null)
            {
                var plateformeIds = await _db.Clients.AsNoTracking()
                    .Where(c => c.Id == clientId)
                    .Select(c => c.PlateformeId)
                    .ToListAsync(ct);

                if (plateformeIds.Count > 0)
                    total += await stocks
                        .Where(s => s.CommandeClientId == null && s.ClientId == null
                                 && s.PlateformeId != null && plateformeIds.Contains(s.PlateformeId.Value))
                        .SumAsync(s => (long)s.Quantite, ct);
            }

            if (groupeIds.Count > 0)
                total += await stocks
                    .Where(s => s.CommandeClientId == null && s.ClientId == null
                             && s.GroupeCommandeId != null && groupeIds.Contains(s.GroupeCommandeId.Value))
                    .SumAsync(s => (long)s.Quantite, ct);

            return total;
        }

        /// <summary>
        /// Metrage de tissu importe disponible pour l'article, sur la commande ou sur
        /// le groupe de commandes.
        ///
        /// On ne compte pas le stock importe non rattache : il est deja pris en compte
        /// par <see cref="CouvertureAsync"/> via son client, sa plateforme ou son
        /// groupe. Compter ici tout le stock importe libre de la base ajouterait le
        /// meme metre deux fois et annoncerait un announced sans rapport avec ce que
        /// la commande peut reellement couvert.
        /// </summary>
        private async Task<decimal> MetrageAnnonceAsync(int commandeId, int articleId, CancellationToken ct)
        {
            var groupeIds = await _db.GroupeCommandeCommandes
                .AsNoTracking()
                .Where(g => g.CommandeClientId == commandeId)
                .Select(g => g.GroupeCommandeId)
                .ToListAsync(ct);

            var q = _db.Stocks.AsNoTracking()
                .Where(s => s.ArticleId == articleId
                         && s.Quantite > 0
                         && s.TypeStock == TypeStock.Importe
                         && (s.CommandeClientId == commandeId
                             || (s.CommandeClientId == null
                                 && s.GroupeCommandeId != null
                                 && groupeIds.Contains(s.GroupeCommandeId.Value))));

            return await q.SumAsync(s => (decimal?)s.Quantite, ct) ?? 0m;
        }

        /// <summary>
        /// Empreinte du triplet (couleur, quantite commandee, quantite couverte).
        /// Volontairement sans le plan de coupe : le manque affiche ne depend que de
        /// ces trois grandeurs, donc une revision du plan ne doit pas rendre la
        /// ligne « perimee ».
        /// </summary>
        private static string CalculerEmpreinte(int quantiteCommandee, long couvert, string couleur)
        {
            var brut = $"{couleur}|{quantiteCommandee}|{couvert}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(brut)))[..16];
        }

        /// <summary>
        /// Couleur comparable : deux saisies libres differentes ne doivent pas
        /// creer deux lignes du meme grain.
        /// </summary>
        private static string Normaliser(string? couleur) =>
            string.IsNullOrWhiteSpace(couleur) ? string.Empty : couleur.Trim().ToUpperInvariant();

        public sealed record ResultatSynchronisation(int Lignes, long QuantiteCommandeeTotale, string? Message);
    }

    /// <summary>Ligne de besoin de coupe relue, avec le manque calcule.</summary>
    public sealed record BesoinCoupeLigne(
        int Id,
        int CommandeClientId,
        int ArticleId,
        string Couleur,
        decimal MetrageAnnonce,
        int QuantiteCommandee,
        long QuantiteCouverte,
        long Manque,
        bool AlerteManque,
        bool EmpreintePerimee);
}
