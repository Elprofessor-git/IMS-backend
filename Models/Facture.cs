using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum StatutFacture
    {
        Brouillon,
        Emise,
        Payee,
        Annulee
    }

    public class Facture
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string NumeroFacture { get; set; } = string.Empty;

        public DateTime DateFacture { get; set; } = DateTime.Now;

        [ForeignKey("Client")]
        public int ClientId { get; set; }

        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";

        [StringLength(50)]
        public string? ModePaiement { get; set; }

        [StringLength(100)]
        public string? Rib { get; set; }

        [StringLength(100)]
        public string? Iban { get; set; }

        [StringLength(50)]
        public string? ModeLivraison { get; set; }

        public int? NombreColis { get; set; }

        public decimal? PoidsNetKg { get; set; }

        public decimal? PoidsBrutKg { get; set; }

        public decimal? VolumeM3 { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Montant total figé en base à l'écriture (convention LigneAchat/LigneImportation) :
        // calculé UNE FOIS à la création/modification des lignes, somme de MontantLigne.
        [Precision(18, 4)]
        public decimal MontantTotal { get; set; } = 0;

        public StatutFacture Statut { get; set; } = StatutFacture.Brouillon;

        public DateTime DateCreation { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? CreePar { get; set; }

        public virtual Client Client { get; set; } = null!;
        public virtual ICollection<FactureCommandeLigne> Lignes { get; set; } = new List<FactureCommandeLigne>();
    }
}