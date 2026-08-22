using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Importation
{
    public class ClotureForceeDto
    {
        [StringLength(500)]
        public string? Raison { get; set; }
    }
}