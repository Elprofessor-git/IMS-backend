using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    // Mise à jour d'en-tête uniquement : 3 champs scalaires modifiables si statut <= 1.
    // Pas de nav-props, pas de champs calculés/workflow.
    public class UpdateCommandeClientDto
    {
        [StringLength(200)]
        public string? TitreCommande { get; set; }

        public DateTime? DateLivraisonSouhaitee { get; set; }

        [StringLength(2000)]
        public string? NotesSpeciales { get; set; }

        /// <summary>Prix de façonnage par pièce (nullable = non défini).</summary>
        [Range(0, double.MaxValue)]
        public decimal? PrixFacon { get; set; }
    }
}
