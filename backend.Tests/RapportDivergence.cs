using Backend_Gestion_Magasin_API.Services.Coupe;

namespace Backend_Gestion_Magasin_API.Tests
{
    /// <summary>
    /// Compare l'en-tête « QT COUPE » d'une feuille du classeur au total que donne
    /// l'échelle, et signale les écarts.
    ///
    /// Pourquoi cet outil vit dans le projet de test et pas dans les services :
    /// l'application N'IMPORTE RIEN du classeur. Les 54 feuilles ne sont que des
    /// cas de test du moteur. Un « rapport d'import » en production serait une
    /// fonctionnalité qui n'a pas lieu d'être.
    ///
    /// Pourquoi ne pas simplementasser l'en-tête : l'en-tête est saisi à la main
    /// et n'est pas toujours reporté après une révision de l'échelle. Sur OVITA il
    /// vaut 4717 alors que l'échelle vaut 4721, et le classeur se contredit
    /// lui-même (voir l'avertissement de la fixture). Forcer l'échelle à coller à
    /// l'en-tête figerait une erreur de saisie ; forcer l'en-tête à coller à
    /// l'échelle prétendrait qu'un fichier tiers est à jour. On garde les deux et on
    /// montre l'écart.
    ///
    /// Aucune valeur d'en-tête n'est donc jamais comparée à une valeur attendue du
    /// moteur : ce rapport est le seul endroit où elles apparaissent.
    /// </summary>
    public static class RapportDivergence
    {
        /// <summary>Une feuille dont l'en-tête du classeur ne vaut pas le total de l'échelle.</summary>
        public sealed record Divergence(
            string Feuille,
            long TotalEchelle,
            long EnTeteClasseur,
            long Ecart,
            long Surplus);

        public sealed record Rapport(IReadOnlyList<Divergence> Divergences, int TotalFeuilles)
        {
            public int Concordantes => TotalFeuilles - Divergences.Count;

            public bool EstVide => Divergences.Count == 0;

            /// <summary>Une ligne par feuille divergente.</summary>
            public IEnumerable<string> Decrire() =>
                Divergences.Select(d =>
                    $"{d.Feuille} : echelle {d.TotalEchelle}, en-tete classeur {d.EnTeteClasseur} " +
                    $"(ecart {d.Ecart}, surplus {d.Surplus})");
        }

        /// <summary>
        /// Construit le rapport. Une feuille sans en-tête n'est jamais signalée : on
        /// n'a rien à comparer.
        /// </summary>
        public static Rapport Construire(IEnumerable<FeuilleImportee> feuilles)
        {
            ArgumentNullException.ThrowIfNull(feuilles);

            var divergences = new List<Divergence>();
            var total = 0;

            foreach (var feuille in feuilles)
            {
                total++;
                if (feuille.EnTeteQtCoupe is not { } enTete) continue;

                var ecart = enTete - feuille.TotalEchelle;
                if (ecart == 0) continue;

                divergences.Add(new Divergence(
                    feuille.Feuille,
                    feuille.TotalEchelle,
                    enTete,
                    ecart,
                    feuille.Surplus));
            }

            divergences.Sort((a, b) => string.CompareOrdinal(a.Feuille, b.Feuille));
            return new Rapport(divergences, total);
        }
    }

    /// <summary>Feuille confrontée : ce que dit l'échelle, ce que dit le classeur.</summary>
    public sealed record FeuilleImportee(
        string Feuille,
        long TotalEchelle,
        long Surplus,
        long? EnTeteQtCoupe);

    /// <summary>Corpus vu par le rapport de divergence.</summary>
    public static class CorpusVersTableau
    {
        public static List<FeuilleImportee> Importer(Corpus corpus)
        {
            var sortie = new List<FeuilleImportee>(corpus.feuilles.Count);

            foreach (var f in corpus.feuilles)
            {
                var (gabarits, passes) = Traduire(f);
                var r = MoteurCoupe.Calculer(gabarits, passes);

                sortie.Add(new FeuilleImportee(
                    f.feuille,
                    r.TotalPlanifie,
                    r.Surplus,
                    f.enTeteClasseur.qtCoupe));
            }

            return sortie;
        }

        public static (Gabarit[] Gabarits, PasseCoupe[] Passes) Traduire(Feuille f)
        {
            var gabarits = f.tailles.Select(t => new Gabarit(t, f.quantites[t])).ToArray();

            var passes = f.passes
                .Select(p => new PasseCoupe(
                    p.numero,
                    p.plis,
                    p.occurrences.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value,
                        StringComparer.OrdinalIgnoreCase)))
                .ToArray();

            return (gabarits, passes);
        }
    }
}
