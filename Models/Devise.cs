using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Devise
    {
        [Key]
        [StringLength(10)]
        public string Code { get; set; } = string.Empty; // PK, ex. "TND", "EUR"
        public string Nom { get; set; } = string.Empty;
        public string Symbole { get; set; } = string.Empty;
        public bool EstActif { get; set; } = true;
    }
}
