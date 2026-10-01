namespace Backend_Gestion_Magasin_API.Services.Email
{
    /// <summary>
    /// Envoi d'un email transactionnel (choix/réinitialisation de mot de passe).
    ///
    /// Contrat volontairement minimaliste : un destinataire, un objet, un corps HTML.
    /// Aucun secret, aucun CC, aucune pièce jointe — ce sont des emails système, pas
    /// une messagerie. Le module Courriels (Gmail) reste, lui, le canal utilisateur
    /// pour envoyer des messages métier : les deux ne se confondent pas et ne
    /// partagent aucun état.
    ///
    /// L'implémentation LÈVE une exception en cas d'échec d'envoi (clé absente,
    /// réseau, domaine d'expéditeur non vérifié). C'est le rôle de
    /// <c>PasswordSetupLinkService</c> — seul point d'entrée applicatif — de
    /// rattraper cette exception : le flux métier qui a déclenché l'email
    /// (création de compte, mot de passe oublié) ne doit jamais s'effondrer parce
    /// qu'un email n'est pas parti.
    /// </summary>
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody);
    }
}
