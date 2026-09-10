using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>Type de chaîne de production (sous-traitance).</summary>
    public enum TypeChaineProduction
    {
        Decoupe,
        Confection,
        Conditionnement
    }

    /// <summary>
    /// Chaîne de production (sous-traitance) vers laquelle les fournitures et les
    /// pièces coupées sont envoyées. Trois chaînes sont créées au seed
    /// (Découpe, Confection, Conditionnement) ; le type est stocké en string.
    /// </summary>
    public class ChaineProduction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        public TypeChaineProduction TypeChaine { get; set; }

        public bool EstActif { get; set; } = true;

        // Relations
        public virtual ICollection<LotExport> LotExports { get; set; } = new List<LotExport>();
        public virtual ICollection<EnvoiFourniture> EnvoisFourniture { get; set; } = new List<EnvoiFourniture>();
    }
}