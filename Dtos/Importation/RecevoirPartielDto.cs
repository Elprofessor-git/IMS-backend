using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Importation
{
    public class RecevoirPartielDto
    {
        [Range(0.01, double.MaxValue)]
        public decimal Quantite { get; set; }
    }
}