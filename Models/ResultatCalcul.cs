using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class ResultatCalcul
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        [ForeignKey("Article")]
        public int ArticleId { get; set; }

        public decimal BesoinBrut { get; set; }
        public decimal MargeAppliquee { get; set; }
        public decimal BesoinFinal { get; set; }
        public decimal QteAchat { get; set; }
        public decimal QteImport { get; set; }
        public decimal QteStockReserve { get; set; }
        public decimal QteDisponible { get; set; }
        public decimal Manque { get; set; }
        public bool EstSuffisant { get; set; }
        public DateTime DateCalcul { get; set; } = DateTime.UtcNow;

        public virtual CommandeClient Commande { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
    }
}
