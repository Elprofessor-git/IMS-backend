using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Ligne de date du planning (colonne de gauche) : date d'export d'une journée
    /// de production, quel que soit le jour de la semaine (généralisation du « samedi »
    /// initial). Une ligne peut exister sans cellule ; chaque cellule du planning est
    /// rattachée à une ligne par sa valeur de date.
    /// </summary>
    public class PlanningDate
    {
        [Key]
        public int Id { get; set; }

        /// <summary>La date de la ligne (libre, pas seulement un samedi).</summary>
        [Required]
        public DateTime Date { get; set; }
    }
}