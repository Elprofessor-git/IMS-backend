using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class DocumentImportation
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Importation")]
        public int ImportationId { get; set; }

        [Required]
        [StringLength(255)]
        public string NomFichier { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string CheminFichier { get; set; } = string.Empty;

        [StringLength(100)]
        public string? TypeFichier { get; set; }

        public long TailleOctets { get; set; }

        public DateTime DateAjout { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? AjoutePar { get; set; }

        public virtual Importation Importation { get; set; } = null!;
    }
}
