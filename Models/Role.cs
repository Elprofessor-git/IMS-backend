using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string NomRole { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        // Permissions par module — écriture
        public bool PeutGererStock { get; set; } = false;
        public bool PeutGererCommandes { get; set; } = false;
        public bool PeutGererTaches { get; set; } = false;
        public bool PeutGererClients { get; set; } = false;
        public bool PeutGererFournisseurs { get; set; } = false;
        public bool PeutGererAchats { get; set; } = false;
        public bool PeutGererImportations { get; set; } = false;
        public bool PeutGererUtilisateurs { get; set; } = false;
        public bool PeutGererMouvements { get; set; } = false;
        public bool PeutGererPlateformes { get; set; } = false;

        // Permissions par module — lecture seule
        public bool PeutVoirMouvements { get; set; } = false;
        public bool PeutVoirCommandes { get; set; } = false;
        public bool PeutVoirClients { get; set; } = false;
        public bool PeutVoirFournisseurs { get; set; } = false;
        public bool PeutVoirPlateformes { get; set; } = false;
        public bool PeutVoirTaches { get; set; } = false;
        public bool PeutVoirUtilisateurs { get; set; } = false;
        public bool PeutVoirRoles { get; set; } = false;

        // Permissions spéciales
        public bool PeutValiderStock { get; set; } = false;
        public bool PeutConfirmerAchats { get; set; } = false;
        public bool PeutValiderImportations { get; set; } = false;
        public bool EstAdministrateur { get; set; } = false;

        // ── Partage de liens (LOT « Partage Sécurisé ») ──────────────────────────
        // Capacité TRANSVERSE, pas un module : elle autorise à créer un lien de
        // consultation hors de l'application, donc à exposer des données à un tiers
        // sans authentification. Elle n'accorde aucun droit de lecture supplémentaire
        // dans l'API — elle permet seulement de rendre lisible ce que le porteur
        // du rôle pouvait déjà voir.
        //
        // Séparée de « stock » / « commandes » / « importations » parce qu'un rôle
        // peut légitimement lire le stock et ne jamais pouvoir le partager.
        public bool PeutPartagerLiens { get; set; } = false;

        // ── Tâches : droits de RESSOURCE (LOT 16) ──────────────────────────────
        // Distincts de la permission de module « taches » (PeutVoirTaches /
        // PeutGererTaches) : ces deux indicateurs portent sur la propriété des données,
        // pas sur l'accès au module.
        // PeutVoirToutesTaches : autorise scope=all et l'accès aux tâches des autres
        //   utilisateurs. Sans ce droit, un utilisateur ne voit que les tâches dont il
        //   est le créateur ou le responsable.
        // PeutAssignerTaches : autorise à affecter une tâche à un autre utilisateur IMS.
        public bool PeutVoirToutesTaches { get; set; } = false;
        public bool PeutAssignerTaches { get; set; } = false;

        // Dashboard & Rapports (lecture seule)
        public bool PeutVoirDashboard { get; set; } = true;
        public bool PeutVoirRapports { get; set; } = true;

        // Facturation
        public bool PeutVoirFactures { get; set; } = false;
        public bool PeutGererFactures { get; set; } = false;

        // Parc machines à coudre (inventaire + maintenance)
        public bool PeutVoirMachines { get; set; } = false;
        public bool PeutGererMachines { get; set; } = false;

        // Module Coupe (suivi transversal des matelas + rapport de coupe)
        public bool PeutVoirCoupe { get; set; } = false;
        public bool PeutGererCoupe { get; set; } = false;
        public bool PeutVoirPlanning { get; set; } = false;
        public bool PeutGererPlanning { get; set; } = false;

        // Module Production (gamme opératoire des OF — LOT 8)
        public bool PeutVoirProduction { get; set; } = false;
        public bool PeutGererProduction { get; set; } = false;

        // Module Qualité (contrôle qualité + cycle retouche — LOT 8)
        public bool PeutVoirQualite { get; set; } = false;
        public bool PeutGererQualite { get; set; } = false;

        // Module Courriels (connexion Gmail, synchronisation, analyse IA, brouillons de réponse)
        public bool PeutVoirCourriels { get; set; } = false;
        public bool PeutGererCourriels { get; set; } = false;

        // Module Planning (grille chaînes × samedis)
                
        public DateTime DateCreation { get; set; } = DateTime.Now;
        
        public bool EstActif { get; set; } = true;
        
        // Relations
        public virtual ICollection<ApplicationUser> Utilisateurs { get; set; } = new List<ApplicationUser>();
    }
}

