using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }

        [Precision(18, 2)] // total digits: 18, decimals: 2
        public decimal Price { get; set; }
    }
}