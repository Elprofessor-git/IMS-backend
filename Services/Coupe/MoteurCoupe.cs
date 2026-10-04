using System.Globalization;

namespace Backend_Gestion_Magasin_API.Services.Coupe
{
    /// <summary>
    /// Moteur de coupe partagé. Reconstitue l'échelle d'« ORDRE DE COUPE-2026 »
    /// sous forme de fonction pure : aucun accès base, aucune horloge, aucun aléa.
    ///
    /// L'échelle est une échelle de restes. On part de la quantité commandée par
    /// gabarit, puis chaque passe (un matelas) en retire plis × occurrences. Les
    /// restes négatifs sont CONSERVÉS tels quels : un gabarit trop coupé n'est pas
    /// ramené à zéro, c'est un surplus, et l'écart se voit.
    ///
    ///   reste(0, g) = quantiteCommandee(g)
    ///   reste(t, g) = reste(t-1, g) - plis(t) × occurrences(t, g)
    ///   totalPlanifie = Σ plis(t) × Σ_g occurrences(t, g)
    ///
    /// Invariant : totalPlanifie = quantiteCommandee + surplus - manque.
    /// 5 des 54 feuilles du classeur sous-couvrent la commande (DDRD7-D321 : 80
    /// planifiés pour 1200) ; le manque est un état normal, pas une erreur.
    /// </summary>
    public static class MoteurCoupe
    {
        /// <summary>Lance l'échelle et renvoie l'état final, gabarit par gabarit.</summary>
        public static ResultatCoupe Calculer(IReadOnlyList<Gabarit> gabarits, IReadOnlyList<PasseCoupe> passes)
        {
            ArgumentNullException.ThrowIfNull(gabarits);
            ArgumentNullException.ThrowIfNull(passes);

            var restes = gabarits.ToDictionary(
                g => g.Taille,
                g => g.QuantiteCommandee,
                StringComparer.OrdinalIgnoreCase);

            var lignes = new List<LigneResultatCoupe>(passes.Count);
            long totalPlanifie = 0;

            foreach (var passe in passes)
            {
                // Une passe reste comptée même pour un gabarit absent du plan : le
                // métrage ne se neutralise pas en silence, l'écart se voit.
                long metrage = 0;
                var contributions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var (taille, occurrences) in passe.Occurrences)
                {
                    if (occurrences <= 0) continue;
                    // Overflow explicite plutôt qu'un débordement silencieux : un
                    // gabarit dessiné des millions de fois est une saisie erronee,
                    // pas une quantité.
                    var contribution = checked((int)((long)passe.Plis * occurrences));
                    metrage += contribution;
                    contributions[taille] = contribution;
                }

                foreach (var taille in restes.Keys.ToList())
                {
                    var occ = passe.Occurrences.TryGetValue(taille, out var v) ? v : 0;
                    if (occ <= 0) continue;
                    // Le retrait est borné comme la contribution : un reste très
                    // négatif reste représentable, un débordement doit être bruyant
                    // plutôt que de boucler silencieusement.
                    var retrait = checked((int)((long)passe.Plis * occ));
                    restes[taille] = checked(restes[taille] - retrait);
                }

                totalPlanifie += metrage;

                lignes.Add(new LigneResultatCoupe(
                    passe.Numero,
                    passe.Libelle,
                    passe.Plis,
                    metrage,
                    contributions,
                    new Dictionary<string, int>(restes, StringComparer.OrdinalIgnoreCase)));
            }

            var quantiteCommandee = gabarits.Sum(g => (long)g.QuantiteCommandee);
            var ecart = totalPlanifie - quantiteCommandee;
            var surplus = Math.Max(0, ecart);
            var manque = Math.Max(0, -ecart);

            return new ResultatCoupe(
                quantiteCommandee,
                totalPlanifie,
                surplus,
                manque,
                new Dictionary<string, int>(restes, StringComparer.OrdinalIgnoreCase),
                lignes);
        }
    }

    /// <summary>Un gabarit (taille) de la commande et sa quantité commandée.</summary>
    public sealed record Gabarit(string Taille, int QuantiteCommandee);

    /// <summary>
    /// Une passe de l'échelle, c'est-à-dire un matelas : <see cref="Plis"/> plis,
    /// et pour chaque gabarit le nombre d'emplacements dessinés sur la nappe.
    /// L'ordre du dictionnaire n'a aucune importance : une passe retire en même
    /// temps sur tous les gabarits, il n'y a pas de séquencement par taille.
    /// </summary>
    public sealed record PasseCoupe(
        int Numero,
        int Plis,
        IReadOnlyDictionary<string, int> Occurrences,
        string? Libelle = null);

    /// <summary>État de l'échelle après une passe.</summary>
    public sealed record LigneResultatCoupe(
        int Numero,
        string? Libelle,
        int Plis,
        long Metrage,
        /// <summary>Quantité produite par cette passe, par gabarit : plis × occurrences.</summary>
        IReadOnlyDictionary<string, int> Contributions,
        /// <summary>Reste par gabarit APRÈS cette passe.</summary>
        IReadOnlyDictionary<string, int> Restes);

    /// <summary>État final de l'échelle.</summary>
    public sealed record ResultatCoupe(
        long QuantiteCommandee,
        long TotalPlanifie,
        long Surplus,
        long Manque,
        IReadOnlyDictionary<string, int> Restes,
        IReadOnlyList<LigneResultatCoupe> Passes)
    {
        /// <summary>Vrai si la commande n'est pas entièrement planifiée.</summary>
        public bool AlerteManque => Manque > 0;

        /// <summary>Invariant vérifié par les tests : total = commande + surplus - manque.</summary>
        public bool EstCoherent =>
            TotalPlanifie == QuantiteCommandee + Surplus - Manque;

        public string Decrire() =>
            $"commandé {QuantiteCommandee}, planifié {TotalPlanifie}, " +
            $"surplus {Surplus}, manque {Manque}";
    }
}
