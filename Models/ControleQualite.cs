using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Nature du contrôle qualité (conception v1.1 §4.2).
    /// Interne : 1er contrôle d'un lot reçu (TypeControle sans parent) OU
    /// re-contrôle d'une retouche traitée sur place (chaîne interne).
    /// RetourSousTraitant : re-contrôle d'un renvoi de retouche (cycle 4-5).
    /// </summary>
    public enum TypeControle
    {
        Interne,
        RetourSousTraitant
    }

    /// <summary>
    /// Contrôle qualité — réception + contrôle en UNE saisie.
    /// (conception v1.1 §4.2)
    /// Invariant de somme : QuantiteAcceptee + QuantiteRetouche + QuantiteRebut
    /// = QuantiteControlee (chaque terme ≥ 0), vérifié à la saisie puis immuable
    /// (aucun update/delete après validation).
    /// Rattachement provisoire à OrdreFabricationId (5.3 non livré) — deviendra
    /// OrdreFabricationEtapeId après la gamme opératoire.
    /// </summary>
    public class ControleQualite
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrdreFabrication")]
        public int? OrdreFabricationId { get; set; }

        [ForeignKey("ChaineProduction")]
        public int? ChaineProductionId { get; set; }

        public TypeControle TypeControle { get; set; } = TypeControle.Interne;

        /// <summary>Contrôle dont ce contrôle est le re-contrôle (NULL = tour 1).</summary>
        [ForeignKey("ControleParent")]
        public int? ControleParentId { get; set; }

        /// <summary>Renvoi précis dont ce contrôle re-contrôle les pièces (tour N+1).</summary>
        [ForeignKey("EnvoiRetouche")]
        public int? EnvoiRetoucheId { get; set; }

        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int QuantiteControlee { get; set; }

        public int QuantiteAcceptee { get; set; }

        public int QuantiteRetouche { get; set; }

        public int QuantiteRebut { get; set; }

        public DateTime DateControle { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Relations
        public virtual OrdreFabrication? OrdreFabrication { get; set; }
        public virtual ChaineProduction? ChaineProduction { get; set; }
        public virtual ControleQualite? ControleParent { get; set; }
        public virtual ICollection<ControleQualite> ControlesEnfants { get; set; } = new List<ControleQualite>();
        public virtual EnvoiRetouche? EnvoiRetouche { get; set; }
        public virtual ICollection<EnvoiRetouche> EnvoisRetouche { get; set; } = new List<EnvoiRetouche>();
        public virtual ICollection<ControleQualiteDefautLigne> Defauts { get; set; } = new List<ControleQualiteDefautLigne>();
    }
}