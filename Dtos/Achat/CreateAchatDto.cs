using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Achat
{
    public class CreateAchatDto
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

        [StringLength(100)]
        public string? CreePar { get; set; }
    }
}
