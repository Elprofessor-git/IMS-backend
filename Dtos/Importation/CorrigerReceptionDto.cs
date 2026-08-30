using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Importation
{
    public class CorrigerReceptionDto
    {
        [Range(0, double.MaxValue)]
        public decimal NouvelleQuantiteRecue { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string Justification { get; set; } = string.Empty;
    }
}
