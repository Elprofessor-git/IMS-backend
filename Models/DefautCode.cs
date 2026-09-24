using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Table de référence des codes défauts (conception v1.1 §4.2).
    /// </summary>
    public class DefautCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Libelle { get; set; } = string.Empty;

        public bool EstActif { get; set; } = true;
    }
}