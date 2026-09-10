using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class LotExport
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        /// <summary>
        /// Chaîne de production destinataire des pièces (sous-traitance multi-chaînes).
        /// Nullable et additif : les exports existants restent valides sans chaîne.
        /// FILTRE/AFFICHAGE uniquement — le plafond d'export reste global (Σ export ≤ Σ coupe).
        /// La suppression d'une chaîne met la référence à NULL (SetNull).
        /// </summary>
        [ForeignKey("ChaineProduction")]
        public int? ChaineProductionId { get; set; }

        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int QuantiteExportee { get; set; }

        public DateTime DateExport { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        public bool ForcerDepassement { get; set; } = false;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public virtual CommandeClient Commande { get; set; } = null!;
        public virtual ChaineProduction? ChaineProduction { get; set; }
    }
}