using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Importation
{
    // Mise à jour d'en-tête uniquement : pas de LignesImportation ni de champs gérés par le
    // workflow (Statut, MontantTotal, DateReceptionReelle, chemins de documents…).
    // Même défaut que PutAchat : binder l'entité EF expose des navs non-nullables en
    // [Required] implicites et écrase des champs serveur si le client les renvoie.
    public class UpdateImportationDto
    {
        public int? FournisseurId { get; set; }

        public int? PlateformeId { get; set; }

        public DateTime? DateReceptionPrevue { get; set; }

        public ModeExpedition ModeExpedition { get; set; } = ModeExpedition.Maritime;

        [StringLength(10)]
        public string? Devise { get; set; }

        [StringLength(1000)]
        public string? NotesImportation { get; set; }
    }
}
