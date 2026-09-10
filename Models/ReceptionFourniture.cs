using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Réception de fourniture (entrée en stock de la fourniture commandée).
    /// Cumulatif et sans plafond : il n'y a aucune limite de dépassement sur
    /// les réceptions — seul l'envoi vers une chaîne est plafonné.
    /// </summary>
    public class ReceptionFourniture
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("CommandeLigne")]
        public int CommandeFournitureLigneId { get; set; }

        public decimal QuantiteRecue { get; set; }

        public DateTime DateReception { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relations
        public virtual FournitureCommandeLigne CommandeLigne { get; set; } = null!;
    }
}