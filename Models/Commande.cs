
namespace Backend_Gestion_Magasin.Models
{
    public class Commande
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public required List<Product> Products { get; set; }
    }
}