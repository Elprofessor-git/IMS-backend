using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Ligne du plan de coupe (marker) d'un matelas : une taille dessinée sur la nappe,
    /// avec son nombre d'occurrences. La quantité théorique du matelas pour cette taille
    /// = Occurrences × Matelas.PiecePliage (le matelas est coupé en n couches).
    /// Le plan de coupe agrégé est la référence *opérateur* de l'atelier (Conception §A.4) :
    /// le garde-fou administratif de saisie des coupes reste ConfigTaille × marge.
    /// Immuable dès que le matelas porte des coupes enregistrées (409 — l'historique ne se réécrit pas).
    /// </summary>
    public class PlanDeCoupeLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Matelas")]
        public int MatelasId { get; set; }

        /// <summary>Gabarit dessiné sur la nappe (ex. « M »). Doit exister dans ConfigTaille de la commande.</summary>
        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        /// <summary>Nombre d'emplacements de ce gabarit sur la nappe (≥ 1).</summary>
        [Range(1, int.MaxValue)]
        public int Occurrences { get; set; } = 1;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public virtual Matelas? Matelas { get; set; }
    }
}