using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Prenom { get; set; }

        [StringLength(100)]
        public string? Poste { get; set; }
        
        [ForeignKey("Role")]
        public int? RoleId { get; set; }
        
        [StringLength(100)]
        public string? Equipe { get; set; }
        
        [StringLength(100)]
        public string? Departement { get; set; }
        
        public DateTime? DerniereConnexion { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public bool EstActif { get; set; } = true;

        // ── Module Discussion E1 ────────────────────────────────────────────────
        // Marqueur « compte interne » (invité par un administrateur, aucun
        // self-signup possible aujourd'hui). Un futur compte EXTERNE (partenaire,
        // client) devra être créé avec EstCompteInterne = false : il sera alors
        // exclu de l'annuaire de la Discussion et refusé sur tous ses endpoints.
        // En cas de doute sur le type de compte, la règle est le REFUS.
        public bool EstCompteInterne { get; set; } = true;
        
        // Relations
        public virtual Role? Role { get; set; }

        // Module Courriels : comptes Gmail connectés par cet utilisateur IMS
        public virtual ICollection<Models.Gmail.GmailConnection> GmailConnections { get; set; } = new List<Models.Gmail.GmailConnection>();
    }
}