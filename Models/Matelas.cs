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

        /// <summary>Commande dont relève ce matelas (frappe obligatoire à la création).</summary>
        [Required]
        public int CommandeId { get; set; }

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

        /// <summary>Commande porteuse du matelas (scope).</summary>
        public virtual CommandeClient? Commande { get; set; }

        // Relations : pièces réellement coupées à partir de ce matelas
        public virtual ICollection<LotCoupe> LotCoupes { get; set; } = new List<LotCoupe>();
    }
}