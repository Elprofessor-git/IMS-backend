using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Ordre de fabrication (Phase 1 — structure &amp; étiquetage).
    /// Couche de structure scoping CommandeClient : chaque OF porte sa propre
    /// répartition par taille (OrdreFabricationTailleLigne), sa chaîne de
    /// production assignée et ses étiquettes/fournitures étiquetées.
    /// Le calcul de couverture (Calculer/ValiderRessources) reste au niveau
    /// commande globale : aucun code de calcul ne référence ce modèle.
    /// </summary>
    public class OrdreFabrication
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Commande")]
        public int CommandeId { get; set; }

        /// <summary>Numéro d'OF, unique au sein de la commande (index composé CommandeId+NumeroOF).</summary>
        [Required]
        [StringLength(50)]
        public string NumeroOF { get; set; } = string.Empty;

        /// <summary>Chaîne de production assignée (interne ou sous-traitant). Nullable : l'OF
        /// reste valide sans chaîne ; le rattachement est un choix de l'atelier.</summary>
        [ForeignKey("ChaineProduction")]
        public int? ChaineProductionId { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public DateTime? DateMiseAJour { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? CreePar { get; set; }

        [StringLength(100)]
        public string? ModifiePar { get; set; }

        // Relations
        public virtual CommandeClient Commande { get; set; } = null!;
        public virtual ChaineProduction? ChaineProduction { get; set; }
        public virtual ICollection<OrdreFabricationTailleLigne> Tailles { get; set; } = new List<OrdreFabricationTailleLigne>();
        public virtual ICollection<OrdreFabricationEtiquette> Etiquettes { get; set; } = new List<OrdreFabricationEtiquette>();
    }
}