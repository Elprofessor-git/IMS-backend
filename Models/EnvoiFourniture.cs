using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Envoi de fourniture vers une chaîne de production (sous-traitance).
    /// Plafond GLOBAL sur l'ensemble des chaînes : Σ envois ≤ Σ réceptions
    /// de la ligne. Un dépassement n'est autorisé que si ForcerDepassement
    /// est vrai (schéma identique au module Rapport de coupe). ChaineProductionId
    /// est nullable pour les données rétroactives (fiche informative uniquement).
    /// </summary>
    public class EnvoiFourniture
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("CommandeLigne")]
        public int CommandeFournitureLigneId { get; set; }

        [ForeignKey("ChaineProduction")]
        public int? ChaineProductionId { get; set; }

        public decimal QuantiteEnvoyee { get; set; }

        public DateTime DateEnvoi { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        public bool ForcerDepassement { get; set; } = false;

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relations
        public virtual FournitureCommandeLigne CommandeLigne { get; set; } = null!;
        public virtual ChaineProduction? ChaineProduction { get; set; }
    }
}