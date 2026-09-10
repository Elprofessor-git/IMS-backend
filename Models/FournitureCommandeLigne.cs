using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>Portée d'une ligne de fourniture : quantité commune à toutes les tailles
    /// ou quantité par taille (Taille obligatoire dans ce second cas).</summary>
    public enum PorteeFourniture
    {
        Commune,
        ParTaille
    }

    /// <summary>
    /// Ligne de fourniture liée aux pièces coupées d'une commande (module
    /// « Fournitures liées aux pièces coupées »). Le besoin est calculé à la
    /// volée à partir des quantités réellement coupées (cumul LotCoupes) —
    /// jamais à partir de ConfigTailles.
    /// </summary>
    public class FournitureCommandeLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        [ForeignKey("Article")]
        public int ArticleId { get; set; }

        /// <summary>Commune = 1 quantité pour tout le volume coupé ; ParTaille = quantité par taille.</summary>
        public PorteeFourniture Portee { get; set; } = PorteeFourniture.Commune;

        [StringLength(200)]
        public string? DesignationSpecifique { get; set; }

        /// <summary>Quantité de fourniture par pièce coupée (Portee Commune) ou par pièce de la taille (ParTaille).</summary>
        public decimal QuantiteFourniture { get; set; }

        [StringLength(50)]
        public string? Taille { get; set; }

        [StringLength(20)]
        public string? Unite { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relations
        public virtual CommandeClient Commande { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
        public virtual ICollection<ReceptionFourniture> Receptions { get; set; } = new List<ReceptionFourniture>();
        public virtual ICollection<EnvoiFourniture> Envois { get; set; } = new List<EnvoiFourniture>();
    }
}