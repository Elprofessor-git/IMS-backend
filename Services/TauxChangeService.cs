using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    /// <summary>
    /// Résolution du taux de change à figer au moment de l'écriture d'un document
    /// (approche alignée ERP : conversion calculée une seule fois à l'écriture, jamais
    /// recalculée à la lecture — les rapports somment directement les colonnes TND).
    ///
    /// Règle : si <paramref name="devise"/> == "TND" (devise de référence système), le
    /// taux est implicitement 1 (Phase 2 : rebut d'un TauxChange explicite pour TND).
    /// Sinon, on prend le TauxChange le plus proche en date (antérieur OU postérieur) au
    /// document — la table étant minuscule (par devise/date), le chargement en mémoire est
    /// le plus fiable (Math.Abs n'est pas traduit de façon fiable par EF Core en SQL).
    ///
    /// LIMITE CONNUE (documentée) : si aucune devise n'a aucun TauxChange saisi, le fallback
    /// est 1 (aucune conversion) — à documenter car c'est un point ouvert avec Sof.
    /// </summary>
    public static class TauxChangeService
    {
        public static async Task<decimal> ObtenirTauxAsync(
            ApplicationDbContext context,
            string? devise,
            DateTime date)
        {
            if (string.IsNullOrEmpty(devise) || devise == "TND")
            {
                // Devise de référence TND : taux implicite = 1.
                return 1m;
            }

            var taux = await context.TauxChanges
                .Where(t => t.DeviseCode == devise)
                .ToListAsync();

            if (taux.Count == 0)
            {
                // Aucun taux saisi du tout pour cette devise — fallback neutre (1).
                // LIMITATION CONNUE : conversion annulée, montant TND = montant brut.
                return 1m;
            }

            var plusProche = taux
                .OrderBy(t => Math.Abs((t.DateEffective.Date - date.Date).TotalDays))
                .First();

            return plusProche.Taux;
        }
    }
}
