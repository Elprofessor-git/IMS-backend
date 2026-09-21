using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Gestion_Magasin_API.Models
{
    /// <summary>
    /// Notification d'un utilisateur : « Le planning a changé : cellule X pour le
    /// samedi Y, commande Z ». Chaque utilisateur reçoit sa propre ligne de
    /// notification et peut la marquer livrée (lue) individuellement.
    /// </summary>
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UtilisateurId { get; set; } = string.Empty;

        /// <summary>Message textuel de la notification (ex. « Le planning a changé : cellule Découpe pour le 21/09/2026, commande 79-PO33341 »).</summary>
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public DateTime DateNotification { get; set; } = DateTime.Now;

        /// <summary>L'utilisateur a-t-il marqué cette notification comme livrée (lue) ?</summary>
        public bool EstLivree { get; set; }

        /// <summary>Entrée de planning liée (optionnel).</summary>
        public int? PlanningEntryId { get; set; }

        // Relations
        public virtual PlanningEntry? PlanningEntry { get; set; }
    }
}
