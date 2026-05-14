using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class BomLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        [ForeignKey("Article")]
        public int ArticleId { get; set; }

        public decimal QuantiteParPiece { get; set; }

        [StringLength(50)]
        public string? Unite { get; set; }

        public virtual CommandeClient Commande { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
    }
}
