using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos
{
    /// <summary>
    /// Périmètre de lecture du module Tâches. « mine » est TOUJOURS calculé à partir de
    /// l'utilisateur authentifié : le client ne peut pas demander les tâches d'un tiers.
    /// </summary>
    public enum TacheScope
    {
        /// <summary>Tâches dont l'utilisateur est le créateur ou le responsable (défaut).</summary>
        Mine = 0,

        /// <summary>Toutes les tâches. Exige le droit de ressource PeutVoirToutesTaches.</summary>
        All = 1
    }

    /// <summary>
    /// Projection de lecture. N'expose JAMAIS les navigations EF (CommandeClient, CreatedBy,
    /// AssignedTo, MouvementsStock) ni le PasswordHash d'un ApplicationUser : la sérialisation
    /// de l'entité IEnumerable serait une fuite de données d'authentification.
    /// </summary>
    public class TacheReadDto
    {
        public int Id { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CommandeClientId { get; set; }
        public string? EquipeAssignee { get; set; }

        // Libellés legacy ( conservés pour l'affichage et les exports existants).
        public string? ResponsableAssigne { get; set; }
        public string? CreePar { get; set; }
        public string? ModifiePar { get; set; }

        // Ownership — source de vérité. Les deux identifiants sont exposés en lecture seule.
        public string? CreatedByUserId { get; set; }
        public string? AssignedToUserId { get; set; }

        // Libellés lisibles, résolus côté serveur à partir des FK.
        public string? Responsable { get; set; }
        public string? Createur { get; set; }

        public StatutTache Statut { get; set; }
        public PrioriteTache Priorite { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime? DateDebutPrevue { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateDebutReelle { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public int DureeEstimeeHeures { get; set; }
        public int DureeReelleHeures { get; set; }
        public decimal PourcentageAvancement { get; set; }
        public string? NotesProgression { get; set; }
        public string? ProblemesBloques { get; set; }
        public DateTime? DateMiseAJour { get; set; }
        public int? GroupeTacheId { get; set; }

        public TacheCommandeResumeDto? CommandeClient { get; set; }
    }

    public class TacheCommandeResumeDto
    {
        public int Id { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public string? ClientNom { get; set; }
    }

    /// <summary>
    /// Création d'une tâche. Ne contient NI CreatedByUserId, NI les compteurs d'exécution,
    /// NI les champs d'audit : tout ce qui identifie un propriétaire est décidé par le
    /// serveur à partir de l'utilisateur authentifié.
    /// </summary>
    public class CreateTacheProductionDto
    {
        [Required]
        [StringLength(100)]
        public string Titre { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public int? CommandeClientId { get; set; }

        [StringLength(100)]
        public string? EquipeAssignee { get; set; }

        /// <summary>
        /// Optionnel. Absent ou vide => la tâche est assignée à l'utilisateur courant.
        /// Une valeur différente de l'utilisateur courant exige le droit PeutAssignerTaches
        /// et un utilisateur existant et actif : le contrôle est côté serveur.
        /// </summary>
        [StringLength(450)]
        public string? AssignedToUserId { get; set; }

        [StringLength(20)]
        public string? Priorite { get; set; }

        [StringLength(20)]
        public string? Statut { get; set; }

        public DateTime? DateDebutPrevue { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public int DureeEstimeeHeures { get; set; }

        [StringLength(1000)]
        public string? NotesProgression { get; set; }

        public int? GroupeTacheId { get; set; }
    }

    /// <summary>
    /// Mise à jour d'une tâche. Les champs d'ownership (CreatedByUserId, AssignedToUserId)
    /// sont volontairement ABSENTS : l'assignation passe par POST /{id}/Assigner, qui
    /// valide le droit et l'existence du destinataire. Idem pour le statut et l'avancement,
    /// qui ont chacun leur endpoint dédié.
    /// </summary>
    public class UpdateTacheProductionDto
    {
        [Required]
        [StringLength(100)]
        public string Titre { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public int? CommandeClientId { get; set; }

        [StringLength(100)]
        public string? EquipeAssignee { get; set; }

        [StringLength(20)]
        public string? Priorite { get; set; }

        public DateTime? DateDebutPrevue { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public int DureeEstimeeHeures { get; set; }

        [StringLength(1000)]
        public string? NotesProgression { get; set; }
    }

    /// <summary>
    /// Affectation d'une tâche à un utilisateur IMS. L'identifiant est un Id d'AspNetUsers ;
    /// le serveur vérifie qu'il existe, qu'il est actif, et que l'appelant a le droit
    /// d'assigner.
    /// </summary>
    public class AssignerTacheUtilisateurDto
    {
        /// <summary>Id de l'utilisateur destinataire. Null => désassignation.</summary>
        [StringLength(450)]
        public string? AssignedToUserId { get; set; }
    }

    /// <summary>Utilisateur proposé à l'assignation dans l'UI (jamais un ID saisi à la main).</summary>
    public class UtilisateurAssignableDto
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
