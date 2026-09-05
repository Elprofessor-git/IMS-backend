using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeDocument { Facture, BonLivraison, ListeColisage, Autre, BonCommandeProduction }

    public class DocumentJoint
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Achat")]
        public int? AchatId { get; set; }

        [ForeignKey("Importation")]
        public int? ImportationId { get; set; }

        [ForeignKey("CommandeClient")]
        public int? CommandeClientId { get; set; }

        public TypeDocument Type { get; set; } = TypeDocument.Autre;

        [Required]
        [StringLength(255)]
        public string NomFichier { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long TailleOctets { get; set; }

        [Required]
        public byte[] Contenu { get; set; } = Array.Empty<byte>();

        public DateTime DateAjout { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? AjoutePar { get; set; }

        // Nature libre — utilisée uniquement quand Type == Autre (saisie libre du libellé)
        [StringLength(200)]
        public string? Nature { get; set; }

        // Relations — nullable : le modèle est lié à l'un OU l'autre
        public virtual Achat? Achat { get; set; }
        public virtual Importation? Importation { get; set; }
        public virtual CommandeClient? CommandeClient { get; set; }
    }
}
