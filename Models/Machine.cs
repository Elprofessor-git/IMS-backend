using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public enum TypeMachine
    {
        Piqueuse,
        Surjeteuse,
        Recouvreuse,
        Boutonniere,
        Repasseuse,
        Coupe,
        Autre
    }

    public enum StatutMachine
    {
        EnService,
        EnMaintenance,
        HorsService,
        Reforme
    }

    /// <summary>
    /// Machine du parc machines à coudre — module indépendant (inventaire + maintenance).
    /// Ne dépend d'aucun autre module ; aucune méthode protégée concernée.
    /// </summary>
    public class Machine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string CodeMachine { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Marque { get; set; }

        [StringLength(100)]
        public string? Modele { get; set; }

        [StringLength(100)]
        public string? NumeroSerie { get; set; }

        public TypeMachine TypeMachine { get; set; }

        public DateTime? DateAcquisition { get; set; }

        [StringLength(100)]
        public string? Emplacement { get; set; }

        public StatutMachine Statut { get; set; } = StatutMachine.EnService;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public virtual ICollection<InterventionMachine> Interventions { get; set; } = new List<InterventionMachine>();
    }
}