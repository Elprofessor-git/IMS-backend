using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum StatutCommande
    {
        EnAttente,
        Prete,
        EnProduction,
        Terminee,
        Annulee
    }

    /// <summary>
    /// Mode de pilotage de l'atelier (document maître, Partie 3). Détermine la référence
    /// de calcul à suivre pour la répartition par taille : Standard = ConfigTaille figée
    /// dès le départ ; les deux autres cas (atelier sous-traitant pour un tiers, ou
    /// donneur d'ordre faisant produire par un tiers) pilotent sur le cumul réel des
    /// coupes (LotCoupe) au fil des matelas.
    /// </summary>
    public enum ModePilotage
    {
        /// <summary>Production classique : ConfigTaille est la source de vérité connue à l'avance.</summary>
        Standard,

        /// <summary>L'atelier est donneur d'ordre : il fait produire par des sous-traitants.</summary>
        DonneurOrdreSousTraitance,

        /// <summary>L'atelier sous-traite pour un tiers (il produit pour le compte d'un client).</summary>
        SousTraitantPourTiers
    }
    
    public class CommandeClient
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string NumeroCommande { get; set; } = string.Empty;
        
        [ForeignKey("Client")]
        public int ClientId { get; set; }
        
        [StringLength(200)]
        public string? TitreCommande { get; set; }
        
        [StringLength(1000)]
        public string? DescriptionCommande { get; set; }
        
        public DateTime DateCommande { get; set; } = DateTime.Now;
        
        public DateTime? DateLivraisonSouhaitee { get; set; }
        
        public StatutCommande Statut { get; set; } = StatutCommande.EnAttente;

        /// <summary>
        /// Mode de pilotage de l'atelier pour cette commande (Partie 3 du document maître).
        /// Par défaut : Standard. Stocké en string en base (HasConversion&lt;string&gt;),
        /// sérialisé en string vers le frontend.
        /// </summary>
        public ModePilotage ModePilotage { get; set; } = ModePilotage.Standard;
        
        public decimal MontantTotal { get; set; } = 0;
        
        [StringLength(10)]
        public string? Devise { get; set; } = "EUR";
        
        public decimal PourcentageRessourcesCouvertes { get; set; } = 0;

        public decimal MargeSecuriteDefaut { get; set; } = 0;

        /// <summary>Prix de façonnage par pièce (facturation atelier).</summary>
        public decimal? PrixFacon { get; set; }
        
        [StringLength(1000)]
        public string? NotesSpeciales { get; set; }
        
        [StringLength(1000)]
        public string? SpecificationsClient { get; set; }
        
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public DateTime? DateMiseAJour { get; set; }
        
        [StringLength(100)]
        public string? CreePar { get; set; }
        
        [StringLength(100)]
        public string? ModifiePar { get; set; }
        
        // Relations
        public virtual Client Client { get; set; } = null!;
        public virtual ICollection<BesoinCommande> Besoins { get; set; } = new List<BesoinCommande>();
        public virtual ICollection<TacheProduction> Taches { get; set; } = new List<TacheProduction>();
        public virtual ICollection<Achat> Achats { get; set; } = new List<Achat>();
        public virtual ICollection<ConfigTaille> ConfigTailles { get; set; } = new List<ConfigTaille>();
        public virtual ICollection<BomLigne> BomLignes { get; set; } = new List<BomLigne>();
        public virtual ICollection<ResultatCalcul> ResultatsCalcul { get; set; } = new List<ResultatCalcul>();
        public virtual ICollection<LotCoupe> LotCoupes { get; set; } = new List<LotCoupe>();
        public virtual ICollection<LotExport> LotExports { get; set; } = new List<LotExport>();
        public virtual ICollection<FactureCommandeLigne> FacturesLignes { get; set; } = new List<FactureCommandeLigne>();
        public virtual ICollection<FournitureCommandeLigne> FournituresLignes { get; set; } = new List<FournitureCommandeLigne>();
        public virtual ICollection<OrdreFabrication> OrdresFabrication { get; set; } = new List<OrdreFabrication>();
    }
}

