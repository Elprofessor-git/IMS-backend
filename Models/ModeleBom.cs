using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class ModeleBom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public virtual ICollection<FournitureBom> Fournitures { get; set; } = new List<FournitureBom>();
    }

    public class FournitureBom
    {
        [Key]
        public int Id { get; set; }

        public int ModeleBomId { get; set; }
        public virtual ModeleBom ModeleBom { get; set; } = null!;

        public int ArticleId { get; set; }
        public virtual Article Article { get; set; } = null!;

        [Required]
        public decimal QteParPiece { get; set; }

        [StringLength(20)]
        public string Unite { get; set; } = string.Empty;
    }
}
