using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class TauxChange
    {
        [Key]
        public int Id { get; set; }

        [StringLength(10)]
        public string DeviseCode { get; set; } = string.Empty; // FK vers Devise, Restrict

        [ForeignKey(nameof(DeviseCode))]
        public virtual Devise Devise { get; set; } = null!;

        public DateTime DateEffective { get; set; }

        // Nombre de TND pour 1 unité de DeviseCode
        public decimal Taux { get; set; }
    }
}
