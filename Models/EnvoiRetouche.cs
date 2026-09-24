using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Renvoi de retouche vers la chaîne SOUS-TRAITANTE d'origine (conception
    /// v1.1 §4.2). Le renvoi est plafonné en dur à la quantité de retouche du
    /// contrôle source — aucun ForcerDepassement (pas de raison métier légitime
    /// de renvoyer plus que ce qui a été déclaré en retouche).
    /// Garde : ChaineProductionId = ChaineProductionId(ControleQualite source).
    /// </summary>
    public class EnvoiRetouche
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("ControleSource")]
        public int ControleQualiteId { get; set; }

        [ForeignKey("ChaineProduction")]
        public int ChaineProductionId { get; set; }

        public int QuantiteRenvoyee { get; set; }

        public DateTime DateEnvoi { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relations
        public virtual ControleQualite ControleSource { get; set; } = null!;
        public virtual ChaineProduction ChaineProduction { get; set; } = null!;
        public virtual ICollection<ControleQualite> Controles { get; set; } = new List<ControleQualite>();
    }
}