using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.Extensions.Logging;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Mise à jour du prix de référence d'un article (Article.PrixUnitaireMoyen = dernier
    /// prix connu) + écriture de l'entrée HistoriquePrixArticle associée.
    ///
    /// Règle clé : cet enregistrement est TOUJOURS « best effort » — une erreur ici est
    /// loggée puis ignorée afin de ne JAMAIS faire échouer la transaction principale
    /// (création d'une ligne d'achat/importation notamment). Le prix n'intervient ni dans
    /// le calcul de couverture BOM (ValiderRessources/Calculer) ni dans les livraisons/
    /// réceptions (LivrerAchat/RecevoirImportation).
    /// </summary>
    public static class PrixHistoriqueService
    {
        /// <summary>
        /// Si <paramref name="prixUnitaire"/> diffère du prix actuel de l'article :
        /// met à jour Article.PrixUnitaireMoyen et crée une entrée HistoriquePrixArticle.
        /// Ne lève jamais d'exception : les erreurs sont loguées et avalées.
        /// Effectue son propre SaveChangesAsync (appelé APRÈS le SaveChanges principal).
        /// </summary>
        public static async Task EnregistrerPrixAsync(
            ApplicationDbContext context,
            int articleId,
            decimal prixUnitaire,
            string? devise,
            SourcePrix source,
            int? ligneAchatId = null,
            int? ligneImportationId = null,
            ILogger? logger = null)
        {
            try
            {
                var article = await context.Articles.FindAsync(articleId);
                if (article == null)
                {
                    return;
                }

                if (article.PrixUnitaireMoyen == prixUnitaire)
                {
                    // Prix inchangé : aucune entrée d'historique (évite de polluer avec des doublons)
                    return;
                }

                article.PrixUnitaireMoyen = prixUnitaire;

                context.HistoriquesPrixArticles.Add(new HistoriquePrixArticle
                {
                    ArticleId = articleId,
                    PrixUnitaire = prixUnitaire,
                    Devise = devise,
                    DateEffective = DateTime.UtcNow,
                    Source = source,
                    LigneAchatId = ligneAchatId,
                    LigneImportationId = ligneImportationId
                });

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Best effort volontaire : la mise à jour du prix ne doit jamais casser
                // la création de la ligne qui en est à l'origine.
                logger?.LogError(ex,
                    "Échec (ignoré) de la mise à jour du prix de référence de l'article {ArticleId} " +
                    "depuis {Source} (ligne={LigneAchatId}/{LigneImportationId}, prix={PrixUnitaire}).",
                    articleId, source, ligneAchatId, ligneImportationId, prixUnitaire);
            }
        }
    }
}
