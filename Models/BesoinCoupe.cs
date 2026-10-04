using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Besoin de coupe d'une commande, au grain (commande, article, couleur).
    ///
    /// Le grain est triplet et NON la taille : les tailles d'un même article/couleur
    /// se découpent dans le même ordre de coupe, et c'est l'échelle agrégée qui est
    /// commandée, pas chaque gabarit isolément.
    ///
    /// Ce que l'entité ne porte PAS, volontairement :
    ///   • le manque — il se recalcule à la lecture, car il dépend du plan de coupe
    ///     et des coupes réelles, tous deux volatils (voir BesoinCoupeService) ;
    ///   • le surplus — même raison ;
    ///   • aucun cumul historique : une commande recalculée réécrit ses lignes.
    ///
    /// Couche additive : nullable ou non, la table est neuve et vide au démarrage,
    /// il n'y a donc aucun report d'historique à faire.
    /// </summary>
    public class BesoinCoupe
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("CommandeClient")]
        public int CommandeClientId { get; set; }

        [ForeignKey("Article")]
        public int ArticleId { get; set; }

        /// <summary>Couleur demandée. Non null : le grain l'exige.</summary>
        [Required]
        [StringLength(50)]
        public string Couleur { get; set; } = string.Empty;

        /// <summary>Quantité commandée cumulée sur toutes les tailles de la ligne.</summary>
        public int QuantiteCommandee { get; set; }

        /// <summary>Métréage de tissu annoncé pour cette couleur (0 si non textile).</summary>
        public decimal MetrageAnnonce { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        /// <summary>
        /// Empreinte du calcul ayant produit cette ligne. Permet de savoir si le
        /// manque affiché est à jour sans recalculer : compare l'empreinte courante
        /// à celle stockée, et signale l'écart au lieu d'afficher un chiffre faux.
        /// </summary>
        [StringLength(64)]
        public string EmpreinteCalcul { get; set; } = string.Empty;

        public virtual CommandeClient CommandeClient { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
    }
}
