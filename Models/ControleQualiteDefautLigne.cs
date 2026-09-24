using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Ligne de défaut rattachée à un contrôle qualité (conception v1.1 §4.2).
    /// </summary>
    public class ControleQualiteDefautLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("ControleQualite")]
        public int ControleQualiteId { get; set; }

        [ForeignKey("DefautCode")]
        public int DefautCodeId { get; set; }

        public int Quantite { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relations
        public virtual ControleQualite ControleQualite { get; set; } = null!;
        public virtual DefautCode DefautCode { get; set; } = null!;
    }
}