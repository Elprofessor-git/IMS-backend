using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Achat
{
    // Mise à jour d'en-tête uniquement : pas de LignesAchat ni de nav-props (elles sont gérées
    // par les endpoints dédiés /LignesAchat et par le workflow Statut/MontantTotal).
    // Bind l'entité EF directement exposait les navs non-nullables comme [Required]
    // implicites (400 "The Achat field is required.") — voir journal des bugs.
    public class UpdateAchatDto
    {
        [Required]
        public int FournisseurId { get; set; }

        public int? CommandeClientId { get; set; }

        public DateTime? DateLivraisonPrevue { get; set; }

        [StringLength(10)]
        public string? Devise { get; set; }

        [StringLength(500)]
        public string? ConditionsPaiement { get; set; }

        public TypePaiement? TypePaiement { get; set; }

        [StringLength(1000)]
        public string? NotesAchat { get; set; }
    }
}
