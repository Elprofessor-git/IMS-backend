using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    public class CreateCommandeClientDto
    {
        [Required]
        public int ClientId { get; set; }

        [StringLength(200)]
        public string? TitreCommande { get; set; }

        [StringLength(1000)]
        public string? DescriptionCommande { get; set; }

        public DateTime? DateLivraisonSouhaitee { get; set; }

        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";

        [StringLength(1000)]
        public string? NotesSpeciales { get; set; }

        [StringLength(1000)]
        public string? SpecificationsClient { get; set; }

        [StringLength(100)]
        public string? CreePar { get; set; }

        /// <summary>Prix de façonnage par pièce (nullable = non défini).</summary>
        [Range(0, double.MaxValue)]
        public decimal? PrixFacon { get; set; }

        /// <summary>Mode de pilotage atelier (Partie 3). Par défaut : Standard.</summary>
        public ModePilotage? ModePilotage { get; set; }
    }
}
