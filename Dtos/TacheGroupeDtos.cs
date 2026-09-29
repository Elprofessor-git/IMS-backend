using System.ComponentModel.DataAnnotations;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Dtos
{
    // Génération d'une tâche d'application : titre + description des lignes du groupe
    // copiés, avec CommandeClientId renseigné. groupKey : GroupeTacheId d'origine.
    public class GroupeTacheLigneDto
    {
        public int Id { get; set; }
        public int GroupeTacheId { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? EquipeAssignee { get; set; }
        public string? ResponsableAssigne { get; set; }
        public int Priorite { get; set; }
        public int Ordre { get; set; }
        public int DureeEstimeeHeures { get; set; }
    }

    public class GroupeTacheDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool EstActif { get; set; }
        public DateTime DateCreation { get; set; }
        public List<GroupeTacheLigneDto> Lignes { get; set; } = new();
        /// <summary>Nombre de commandes distinctes ayant reçu ce groupe (via tâches générées).</summary>
        public int NombreCommandesAppliquees { get; set; }
        /// <summary>Nombre total de tâches générées depuis ce groupe.</summary>
        public int NombreTachesGenerees { get; set; }
    }

    public class CreateGroupeTacheDto
    {
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;
        [StringLength(1000)]
        public string? Description { get; set; }
    }

    public class UpdateGroupeTacheDto
    {
        [StringLength(100)]
        public string? Nom { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        public bool? EstActif { get; set; }
    }

    public class CreateGroupeTacheLigneDto
    {
        [Required]
        [StringLength(100)]
        public string Titre { get; set; } = string.Empty;
        [StringLength(1000)]
        public string? Description { get; set; }
        [StringLength(100)]
        public string? EquipeAssignee { get; set; }
        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }
        public int Priorite { get; set; }
        public int Ordre { get; set; }
        public int DureeEstimeeHeures { get; set; }
    }

    public class UpdateGroupeTacheLigneDto
    {
        [StringLength(100)]
        public string? Titre { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        [StringLength(100)]
        public string? EquipeAssignee { get; set; }
        [StringLength(100)]
        public string? ResponsableAssigne { get; set; }
        public int? Priorite { get; set; }
        public int? Ordre { get; set; }
        public int? DureeEstimeeHeures { get; set; }
    }

    public class AppliquerGroupeTacheDto
    {
        public int CommandeId { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Issue d'une résolution de responsable (libellé libre → utilisateur IMS)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Issue de la résolution d'un libellé de responsable. Remplace l'ancien
    /// <c>ApplicationUser?</c> : le « non » y signifiait à la fois « aucun
    /// responsable désigné » et « aucun utilisateur trouvé », deux intentions
    /// opposées pour l'appelant. Chaque cas a désormais son propre état.
    /// </summary>
    public enum StatutResolutionResponsable
    {
        /// <summary>
        /// La ligne ne désigne aucun responsable : la tâche revient à son créateur.
        /// Ce n'est PAS un échec — c'est le cas nominal d'un gabarit laissé vide.
        /// </summary>
        NonDesigne = 0,

        /// <summary>Un et un seul utilisateur actif correspond au libellé.</summary>
        Resolu = 1,

        /// <summary>Aucun utilisateur actif ne correspond au libellé (échec, 400).</summary>
        AucunMatch = 2,

        /// <summary>
        /// Plusieurs utilisateurs actifs correspondent au libellé (échec, 400) :
        /// aucun choix arbitraire n'est fait, les candidats sont renvoyés pour
        /// que l'appelant tranche.
        /// </summary>
        Ambigu = 3
    }

    /// <summary>
    /// Résultat de la résolution d'un libellé de responsable. Discriminé par
    /// <see cref="Statut"/> : les invariants (Utilisateur non null si et
    /// seulement si <see cref="StatutResolutionResponsable.Resolu"/>, Candidats
    /// non vide si et seulement si <see cref="StatutResolutionResponsable.Ambigu"/>)
    /// sont garantis par le constructeur privé et les trois fabriques, donc
    /// impossibles à contourner depuis l'appelant.
    /// </summary>
    public sealed class ResolutionResponsable
    {
        /// <summary>Libellé recherché, tel que saisi sur la ligne du groupe.</summary>
        public string Libelle { get; }

        public StatutResolutionResponsable Statut { get; }

        /// <summary>Utilisateur résolu. Non null SI ET SEULEMENT si <see cref="Statut"/> vaut Resolu.</summary>
        public ApplicationUser? Utilisateur { get; }

        /// <summary>
        /// Utilisateurs homonymes. Non vide SI ET SEULEMENT si <see cref="Statut"/>
        /// vaut Ambigu ; jamais plus d'un élément sinon.
        /// </summary>
        public IReadOnlyList<ApplicationUser> Candidats { get; }

        private ResolutionResponsable(
            StatutResolutionResponsable statut,
            string libelle,
            ApplicationUser? utilisateur,
            IReadOnlyList<ApplicationUser> candidats)
        {
            Statut = statut;
            Libelle = libelle;
            Utilisateur = utilisateur;
            Candidats = candidats;
        }

        /// <summary>Libellé vide : aucun responsable désigné, la tâche revient au créateur.</summary>
        public static ResolutionResponsable NonDesignee(string libelle) =>
            new(StatutResolutionResponsable.NonDesigne, libelle, null, Array.Empty<ApplicationUser>());

        /// <summary>Correspondance unique : l'utilisateur à affecter est <paramref name="utilisateur"/>.</summary>
        public static ResolutionResponsable Resolue(string libelle, ApplicationUser utilisateur) =>
            new(StatutResolutionResponsable.Resolu, libelle, utilisateur, Array.Empty<ApplicationUser>());

        /// <summary>Aucune correspondance : l'appelant doit refuser (400) sans créer de tâche.</summary>
        public static ResolutionResponsable SansCorrespondance(string libelle) =>
            new(StatutResolutionResponsable.AucunMatch, libelle, null, Array.Empty<ApplicationUser>());

        /// <summary>Correspondances multiples : l'appelant doit refuser (400) en citant les candidats.</summary>
        public static ResolutionResponsable Multiples(string libelle, IReadOnlyList<ApplicationUser> candidats) =>
            new(StatutResolutionResponsable.Ambigu, libelle, null, candidats);
    }
}