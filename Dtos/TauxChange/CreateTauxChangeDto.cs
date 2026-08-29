using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.TauxChange
{
    public class CreateTauxChangeDto
    {
        [Required]
        [StringLength(10)]
        public string DeviseCode { get; set; } = string.Empty;

        [Required]
        public DateTime DateEffective { get; set; }

        [Range(0.000001, 1000000)]
        public decimal Taux { get; set; } // nombre de TND pour 1 unité de DeviseCode
    }
}
