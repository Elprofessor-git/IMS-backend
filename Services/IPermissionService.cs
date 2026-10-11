namespace Backend_Gestion_Magasin_API.Services
{
    public record ModulePermission(string Module, bool CanAccess, bool CanWrite);

    public interface IPermissionService
    {
        Task<(bool canAccess, bool canWrite)> GetPermissionAsync(string userId, string module);
        Task<IEnumerable<ModulePermission>> GetAllPermissionsAsync(string userId);

        /// <summary>
        /// Droit de RESSOURCE sur le module Tâches : autorise « scope=all » et l'accès
        /// aux tâches dont l'utilisateur n'est ni le créateur ni le responsable.
        /// Distinct de la permission de module GetPermissionAsync(userId, "taches").
        /// </summary>
        Task<bool> CanViewAllTachesAsync(string userId);

        /// <summary>
        /// Droit d'affecter une tâche à un autre utilisateur du système.
        /// </summary>
        Task<bool> CanAssignerTachesAsync(string userId);

        /// <summary>
        /// Capacité transverse de créer des liens de partage en lecture seule.
        /// Résolue depuis Role à CHAQUE appel, jamais depuis le jeton : la révocation
        /// d'un droit doit être immédiate, sans réémission ni liste de révocation.
        /// </summary>
        Task<bool> CanPartagerLiensAsync(string userId);

        /// <summary>
        /// Accès au module Discussion (type Slack) : lecture de ses conversations.
        /// Capacité transverse résolue depuis Role, jamais depuis le jeton.
        /// </summary>
        Task<bool> CanUtiliserDiscussionAsync(string userId);

        /// <summary>
        /// Modération des canaux PUBLICS de la Discussion : création/modération de
        /// ceux-ci uniquement. N'ouvre JAMAIS les DM ni les groupes privés d'autrui,
        /// même pour un administrateur : l'appartenance prime. Capacité transverse
        /// résolue depuis Role, jamais depuis le jeton.
        /// </summary>
        Task<bool> CanGererDiscussionAsync(string userId);

        /// <summary>
        /// Identifiants des utilisateurs ACTIFS ayant l'accès en LECTURE à un module,
        /// administrateurs inclus. Sert à cibler les mentions dans une conversation
        /// contextuelle (E3) : y notifier quelqu'un suppose qu'il peut accéder au
        /// message, c.-à-d. qu'il possède le module de l'objet de la conversation.
        /// </summary>
        Task<IReadOnlyList<string>> UtilisateursAvecAccesModuleAsync(string module);
    }
}
