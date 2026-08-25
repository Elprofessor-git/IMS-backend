using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class GroupeCommandeCommande
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("GroupeCommande")]
        public int GroupeCommandeId { get; set; }
        public virtual GroupeCommande GroupeCommande { get; set; } = null!;

        [ForeignKey("CommandeClient")]
        public int CommandeClientId { get; set; }
        public virtual CommandeClient CommandeClient { get; set; } = null!;
    }
}
