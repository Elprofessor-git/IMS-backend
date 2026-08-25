using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class GroupeCommande
    {
        [Key]
        public int Id { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        public virtual ICollection<GroupeCommandeCommande> Membres { get; set; } = new List<GroupeCommandeCommande>();
    }
}
