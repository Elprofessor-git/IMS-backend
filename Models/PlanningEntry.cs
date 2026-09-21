using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Cellule du planning de production hebdomadaire.
    /// Le planning est une grille « chaînes de production × samedis » :
    ///   • la ligne supérieure = les chaînes de production (colonnes),
    ///   • la colonne gauche = les dates d'export (≈ samedi de chaque semaine),
    ///   • chaque cellule = les commandes de matelas prévues sur cette chaîne ce samedi.
    /// Une cellule contient une seule entrée (une commande de matelas) ; plusieurs
    /// cellules partagent (ChaineProductionId, DateSamedi) pour cumuler les commandes
    /// du même samedi sur la même chaîne.
    /// </summary>
    public class PlanningEntry
    {
        [Key]
        public int Id { get; set; }

        // FK vers la chaîne de production (optionnelle)
        public int? ChaineProductionId { get; set; }

        /// <summary>Le samedi de la semaine d'export (date de la cellule).</summary>
        [Required]
        public DateTime DateSamedi { get; set; }

        /// <summary>Numéro de la commande de matelas placée dans la cellule (ex. « 79-PO33341 »).</summary>
        [Required]
        [StringLength(100)]
        public string NumeroCommande { get; set; } = string.Empty;

        /// <summary>Quantité de matelas prévue dans la cellule (optionnel).</summary>
        public int? Quantite { get; set; }

        /// <summary>La production de cette cellule est-elle livrée ?</summary>
        public bool EstLivree { get; set; }

        /// <summary>Notes optionnelles de la cellule.</summary>
        [StringLength(500)]
        public string? Notes { get; set; }

        // Relation
        public virtual ChaineProduction? ChaineProduction { get; set; }
    }
}
