using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Enveloppe de coupe (empilement de pièces de tissu découpées ensemble).
    /// Une coupe (LotCoupe) peut être rattachée optionnellement à un matelas :
    /// le rattachement sert uniquement de traçabilité/regroupement, il n'ajoute
    /// aucune contrainte sur les quantités coupées (cumul géré par LotCoupes).
    /// </summary>
    public class Matelas
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Commande dont relève ce matelas. Nullable : la frappe applicative (POST /api/Matelas)
        /// exige toujours une commande, mais les matelas historiques partagés entre plusieurs
        /// commandes restent valides avec CommandeId = null (principe additif).
        /// </summary>
        public int? CommandeId { get; set; }

        [Required]
        [StringLength(50)]
        public string NumeroMatelas { get; set; } = string.Empty;

        public DateTime DateMatelas { get; set; } = DateTime.Now;

        /// <summary>Nombre de plis du matelas.</summary>
        public int PiecePliage { get; set; }

        /// <summary>Nombre de pièces découpées estimé à la constitution du matelas.</summary>
        public int CoupeEstimee { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public bool EstActif { get; set; } = true;

        /// <summary>
        /// Longueur de nappe du matelas en mètres (géométrie de la table de coupe).
        /// Optionnelle — saisie libre recommandée (défaut : à la constitution du matelas).
        /// Couche additive L1 : n'apporte aucune contrainte, les matelas existants restent valides.
        /// </summary>
        public decimal? Longueur { get; set; }

        /// <summary>
        /// Laize du tissu pour ce matelas (cm). Héritée en défaut de Article.Laize d'une BOM
        /// tissu de la commande, modifiable librement sur le matelas (Décision Conception §9.6).
        /// </summary>
        public decimal? Laize { get; set; }

        /// <summary>Commande porteuse du matelas (scope, null si matelas partagé/historique).</summary>
        public virtual CommandeClient? Commande { get; set; }

        /// <summary>
        /// Plan de coupe du matelas (marker) : une ligne par taille dessinée sur la nappe,
        /// avec son nombre d'occurrences. Quantité théorique par taille = Occurrences × PiecePliage.
        /// </summary>
        public virtual ICollection<PlanDeCoupeLigne> PlanDeCoupeLignes { get; set; } = new List<PlanDeCoupeLigne>();

        // Relations : pièces réellement coupées à partir de ce matelas
        public virtual ICollection<LotCoupe> LotCoupes { get; set; } = new List<LotCoupe>();
    }
}