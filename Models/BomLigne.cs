using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class BomLigne
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        [ForeignKey("Article")]
        public int ArticleId { get; set; }

        public decimal QuantiteParPiece { get; set; }

        [StringLength(50)]
        public string? Unite { get; set; }

        /// <summary>
        /// Indique si cette ligne BOM correspond à un tissu consommable en mètres
        /// (utilisé dans le module Rapport de coupe pour le calcul du besoin linéaire
        /// et la consommation réelle). Faux par défaut.
        /// </summary>
        public bool EstConsommableTissu { get; set; } = false;

        public virtual CommandeClient Commande { get; set; } = null!;
        public virtual Article Article { get; set; } = null!;
    }
}
