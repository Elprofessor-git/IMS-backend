using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos.Importation
{
    // Pas de ReferenceImportation (générée côté serveur, voir Bug 1 du journal) ni de champs
    // gérés par le workflow (Statut, MontantTotal, DateReceptionReelle, chemins de documents…).
    public class CreateImportationDto
    {
        public int? FournisseurId { get; set; }

        public int? PlateformeId { get; set; }

        public DateTime? DateReceptionPrevue { get; set; }

        public ModeExpedition ModeExpedition { get; set; } = ModeExpedition.Maritime;

        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";

        [StringLength(1000)]
        public string? NotesImportation { get; set; }

        [StringLength(100)]
        public string? CreePar { get; set; }
    }
}
