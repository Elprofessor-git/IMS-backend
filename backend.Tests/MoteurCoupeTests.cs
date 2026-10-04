using System.Text.Json;
using System.Text.Json.Serialization;
using Backend_Gestion_Magasin_API.Services.Coupe;

namespace Backend_Gestion_Magasin_API.Tests
{
    /// <summary>
    /// Tests du moteur de coupe sur le corpus des 54 feuilles d'« ORDRE DE COUPE-2026 ».
    ///
    /// Le classeur n'est JAMAIS lu par les tests : il est extrait une fois dans
    /// Fixtures/ordre-coupe-2026.json. Un test qui litrait le .xlsx serait vert chez
    /// moi et rouge sur la machine du voisin, et surtout il lierait la suite à un
    /// binaire de 380 ko difficile à versionner et à relire en diff.
    /// </summary>
    public class MoteurCoupeTests
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static Corpus Charger()
        {
            var chemin = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ordre-coupe-2026.json");
            var json = File.ReadAllText(chemin);
            return JsonSerializer.Deserialize<Corpus>(json, Options)
                   ?? throw new InvalidOperationException("Fixture de coupe illisible.");
        }

        private static (Gabarit[] gabarits, PasseCoupe[] passes) Traduire(Feuille f)
            => CorpusVersTableau.Traduire(f);

        // ── Le corpus lui-même ────────────────────────────────────────────

        [Fact]
        public void Corpus_ContientLes54Feuilles()
        {
            var corpus = Charger();
            Assert.Equal(54, corpus.feuilles.Count);
        }

        [Theory]
        [InlineData("PDJ22-D676")]
        [InlineData("DDJ36-D128-MARINE")]
        [InlineData("OVITA")]
        public void FeuilleReference_EstPresente(string nom)
        {
            var corpus = Charger();
            Assert.Contains(corpus.feuilles, f => f.feuille == nom);
        }

        // ── Règle des restes ──────────────────────────────────────────────

        [Fact]
        public void ChaqueFeuille_ReproduitLeTotalDuClasseur()
        {
            var corpus = Charger();
            var ecarts = new List<string>();

            foreach (var f in corpus.feuilles)
            {
                var (gabarits, passes) = Traduire(f);
                var resultat = MoteurCoupe.Calculer(gabarits, passes);

                if (resultat.TotalPlanifie != f._attendu.metrage)
                    ecarts.Add($"{f.feuille}: {resultat.TotalPlanifie} != {f._attendu.metrage}");
            }

            Assert.True(ecarts.Count == 0,
                "Totaux divergents du classeur :\n" + string.Join("\n", ecarts));
        }

        [Fact]
        public void ChaqueFeuille_ReproduitLeSurplusEtLeManque()
        {
            var corpus = Charger();
            var ecarts = new List<string>();

            foreach (var f in corpus.feuilles)
            {
                var (gabarits, passes) = Traduire(f);
                var r = MoteurCoupe.Calculer(gabarits, passes);

                if (r.Surplus != f._attendu.surplus || r.Manque != f._attendu.manque)
                    ecarts.Add($"{f.feuille}: surplus {r.Surplus}/{f._attendu.surplus}, " +
                               $"manque {r.Manque}/{f._attendu.manque}");
            }

            Assert.True(ecarts.Count == 0,
                "Surplus ou manques divergents :\n" + string.Join("\n", ecarts));
        }

        [Fact]
        public void ChaqueFeuille_ReproduitLesRestesParGabarit()
        {
            var corpus = Charger();
            var ecarts = new List<string>();

            foreach (var f in corpus.feuilles)
            {
                var (gabarits, passes) = Traduire(f);
                var r = MoteurCoupe.Calculer(gabarits, passes);

                foreach (var gabarit in gabarits)
                {
                    var attendu = f._attendu.restes[gabarit.Taille];
                    var obtenu = r.Restes[gabarit.Taille];
                    if (attendu != obtenu)
                        ecarts.Add($"{f.feuille}/{gabarit.Taille}: {obtenu} != {attendu}");
                }
            }

            Assert.True(ecarts.Count == 0,
                "Restes divergents :\n" + string.Join("\n", ecarts));
        }

        [Fact]
        public void ChaqueFeuille_RespecteLInvariantTotalCommandeSurplusMoinsManque()
        {
            var corpus = Charger();

            foreach (var f in corpus.feuilles)
            {
                var (gabarits, passes) = Traduire(f);
                var r = MoteurCoupe.Calculer(gabarits, passes);

                Assert.True(r.EstCoherent,
                    $"{f.feuille} : {r.Decrire()} (total {r.TotalPlanifie}, " +
                    $"attendu {r.QuantiteCommandee + r.Surplus - r.Manque})");
            }
        }

        // ── Contrôles nominatifs ──────────────────────────────────────────

        [Fact]
        public void Pdj22D676_RestesZeroZeroMoinsUnZeroZero_SurplusUn_Total348()
        {
            var f = Charger().feuilles.Single(x => x.feuille == "PDJ22-D676");
            var (gabarits, passes) = Traduire(f);

            var r = MoteurCoupe.Calculer(gabarits, passes);

            // L'échelle du classeur : l'ordre des gabarits est celui des colonnes A..E.
            Assert.Equal(new[] { 0, 0, -1, 0, 0 },
                f.tailles.Select(t => r.Restes[t]).ToArray());
            Assert.Equal(1, r.Surplus);
            Assert.Equal(0, r.Manque);
            Assert.Equal(347, r.QuantiteCommandee);
            Assert.Equal(348, r.TotalPlanifie);
        }

        [Fact]
        public void Ddj36D128Marine_SurplusZero_Total932()
        {
            var f = Charger().feuilles.Single(x => x.feuille == "DDJ36-D128-MARINE");
            var (gabarits, passes) = Traduire(f);

            var r = MoteurCoupe.Calculer(gabarits, passes);

            Assert.Equal(0, r.Surplus);
            Assert.Equal(932, r.TotalPlanifie);
            Assert.Equal(932, r.QuantiteCommandee);
        }

        [Fact]
        public void Ovita_Surplus127_Total4721_TissuRestant12711Point5()
        {
            // Décision : le moteur applique TOUTES les passes, sans cas particulier.
            // 4721 est le total que le classeur utilise lui-même pour le tissu
            // (U4 -> MT UT 4248,9 -> J17 = 12711,5) et son surplus U5 vaut 127.
            // QT COUPE (J4 = 4717) omet la passe 7, dont le nombre de pièces
            // n'est pas renseigné dans cette colonne.
            var f = Charger().feuilles.Single(x => x.feuille == "OVITA");
            var (gabarits, passes) = Traduire(f);

            var r = MoteurCoupe.Calculer(gabarits, passes);

            Assert.Equal(4594, r.QuantiteCommandee);
            Assert.Equal(4721, r.TotalPlanifie);
            Assert.Equal(127, r.Surplus);
            Assert.Equal(0, r.Manque);

            // Le tissu de reference suit l'echelle, pas l'en-tete : 16960,4 recus
            // moins 4721 x 0,9 = 12711,5 restants.
            Assert.Equal(16960.4d, f.tisRecu!.Value, 3);
            Assert.Equal(0.9d, f.consoTissu!.Value, 3);
            Assert.Equal(12711.5d, f.enTeteClasseur.tissuRestant!.Value, 3);
        }

        [Fact]
        public void Ovita_QtCoupeDuClasseur_ValitQuatreDeMoins_QueLEchelle()
        {
            // Ce test ne valide pas l'en-tete : il constate que QT COUPE (J4) vaut
            // 4 de moins que l'echelle, parce que le nombre de pieces de la passe 7
            // n'est pas renseigne dans cette colonne. La divergence reste visible,
            // elle n'est ni forcee ni masquee. Si une revision du classeur renseigne
            // cette cellule, ce test echoue et force a reviser la decision.
            var f = Charger().feuilles.Single(x => x.feuille == "OVITA");
            var (gabarits, passes) = Traduire(f);
            var r = MoteurCoupe.Calculer(gabarits, passes);

            Assert.Equal(-4, f.enTeteClasseur.qtCoupe - r.TotalPlanifie);
        }

        // ── Écart en-tête / échelle ───────────────────────────────────────

        [Fact]
        public void RapportDivergence_NumeroLesFeuillesDontLEchelleDifferedeLEchelleClasseur()
        {
            var rapport = RapportDivergence.Construire(CorpusVersTableau.Importer(Charger()));

            Assert.Equal(new[] { "OVITA" }, rapport.Divergences.Select(d => d.Feuille));
        }

        [Fact]
        public void RapportDivergence_SaufOVITA_Les53AutresFeuillesConcordent()
        {
            var rapport = RapportDivergence.Construire(CorpusVersTableau.Importer(Charger()));

            Assert.Equal(54, rapport.TotalFeuilles);
            Assert.Equal(53, rapport.Concordantes);
            // Le.detail de la divergence d'OVITA : echelle 4721, en-tete 4717.
            var ovita = Assert.Single(rapport.Divergences);
            Assert.Equal(4721, ovita.TotalEchelle);
            Assert.Equal(4717, ovita.EnTeteClasseur);
            Assert.Equal(-4, ovita.Ecart);
        }

        // ── Cellule QT saisie a la main ───────────────────────────────────

        [Theory]
        [InlineData("PDR98 D649-BEIGE", 76, 75)]
        [InlineData("PDR98 D649-BLEU GRISE", 294, 291)]
        public void QuantiteCommandee_LitLaSommeDesQuantites_PasLaCelluleQT
            (string nom, int qtSaisi, int somme)
        {
            // Deux feuilles ont une cellule « QT » saisie a la main qui ne vaut pas
            // la somme que le classeur calcule lui-meme (I4 = T4). Le moteur lit la
            // somme des quantites par gabarit : c'est la valeur que le classeur
            // recalcule, donc la seule qui ne depende pas d'une saisie. On fige ici
            // l'ecart pour qu'il reste visible plutot que d'etre corrige en silence.
            var f = Charger().feuilles.Single(x => x.feuille == nom);
            var (gabarits, passes) = Traduire(f);
            var r = MoteurCoupe.Calculer(gabarits, passes);

            Assert.Equal(qtSaisi, f.enTeteClasseur.qtSaisi);
            Assert.Equal(somme, f.quantites.Values.Sum());
            Assert.Equal(somme, r.QuantiteCommandee);
            Assert.True(qtSaisi > somme, "La cellule QT devrait rester acima de la somme.");
        }

        // ── Règle elle-même, hors corpus ──────────────────────────────────

        [Fact]
        public void ResteNegatif_EstConserve_IlCompteCommeSurplus()
        {
            // 10 demandés, un matelas de 3 plis × 4 occurrences = 12 produits.
            var r = MoteurCoupe.Calculer(
                [new Gabarit("M", 10)],
                [new PasseCoupe(1, 3, new Dictionary<string, int> { ["M"] = 4 })]);

            Assert.Equal(-2, r.Restes["M"]);   // pas ramené à zéro
            Assert.Equal(12, r.TotalPlanifie);
            Assert.Equal(2, r.Surplus);
            Assert.Equal(0, r.Manque);
            Assert.False(r.AlerteManque);
        }

        [Fact]
        public void Manque_EstAlerte_QuandLEchelleSousCouvreLaCommande()
        {
            // Cas DDRD7-D321 : 80 planifiés pour 1200 commandés.
            var r = MoteurCoupe.Calculer(
                [new Gabarit("M", 1200)],
                [new PasseCoupe(1, 4, new Dictionary<string, int> { ["M"] = 20 })]);

            Assert.Equal(80, r.TotalPlanifie);
            Assert.Equal(1120, r.Manque);
            Assert.Equal(0, r.Surplus);
            Assert.True(r.AlerteManque);
        }

        [Fact]
        public void OrdreDesPasses_NAffecteNiLesRestesNiLeTotal()
        {
            // Piège que la règle des restes laisse croire : chaque passe retranche
            // plis x occurrences, donc l'etat final est une SOMME de retraits.
            // L'ordre des matelas est donc sans effet sur le total et
            // sur les restes. Seules les lignes intermédiaires changent.
            var dansLordre = MoteurCoupe.Calculer(
                [new Gabarit("M", 10), new Gabarit("L", 10)],
                [
                    new PasseCoupe(1, 3, new Dictionary<string, int> { ["M"] = 2, ["L"] = 5 }),
                    new PasseCoupe(2, 2, new Dictionary<string, int> { ["M"] = 4, ["L"] = 1 }),
                ]);

            var dansLordreInverse = MoteurCoupe.Calculer(
                [new Gabarit("M", 10), new Gabarit("L", 10)],
                [
                    new PasseCoupe(1, 2, new Dictionary<string, int> { ["M"] = 4, ["L"] = 1 }),
                    new PasseCoupe(2, 3, new Dictionary<string, int> { ["M"] = 2, ["L"] = 5 }),
                ]);

            Assert.Equal(dansLordre.TotalPlanifie, dansLordreInverse.TotalPlanifie);
            Assert.Equal(dansLordre.Surplus, dansLordreInverse.Surplus);
            Assert.Equal(-4, dansLordre.Restes["M"]);   // 10 - (3x2 + 2x4) = -4
            Assert.Equal(-7, dansLordre.Restes["L"]);  // 10 - (3x5 + 2x1) = -7
            Assert.Equal(dansLordre.Restes, dansLordreInverse.Restes);

            // L'intermediaire, lui, depend de l'ordre.
            Assert.Equal(4, dansLordre.Passes[0].Restes["M"]);
            Assert.Equal(2, dansLordreInverse.Passes[0].Restes["M"]);
        }

        [Fact]
        public void SurplusEtManque_NAjdifferentPAS_SurLeTotal_Global()
        {
            // La regle demandee est GLOBALE : surplus et manque se calculent sur les
            // totaux, pas gabarit par gabarit. Ici le total planifie depasse la
            // commande, donc aucun manque n'est signale, meme si le gabarit L n'a
            // jamais ete couvert. C'est une consequence assumee de la regle, et ce
            // test la fige pour qu'on ne la decouvre pas en production.
            var r = MoteurCoupe.Calculer(
                [
                    new Gabarit("M", 100),
                    new Gabarit("L", 10),
                ],
                [new PasseCoupe(1, 5, new Dictionary<string, int> { ["M"] = 30 })]);

            Assert.Equal(110, r.QuantiteCommandee);  // 100 + 10
            Assert.Equal(150, r.TotalPlanifie);      // 5 x 30
            Assert.Equal(-50, r.Restes["M"]);        // 100 - 150, surplus
            Assert.Equal(10, r.Restes["L"]);         // 10 non couvert
            Assert.Equal(40, r.Surplus);             // 150 - 110, pas 50
            Assert.Equal(0, r.Manque);               // le surplus global masque L
            Assert.False(r.AlerteManque);
            Assert.True(r.EstCoherent);
        }

        [Fact]
        public void Manque_EstGlobal_LuiAussi_UnGabaritIntactNeComptePas()
        {
            // Symetrique du test precedent : le manque est global lui aussi.
            // Ici L n'a ete coupe d'aucun et M est trop coupe de 50 ; le
            // manque porte sur le total (50), pas sur le gabarit intact.
            var r = MoteurCoupe.Calculer(
                [
                    new Gabarit("M", 100),
                    new Gabarit("L", 100),
                ],
                [new PasseCoupe(1, 1, new Dictionary<string, int> { ["M"] = 150 })]);

            Assert.Equal(200, r.QuantiteCommandee);
            Assert.Equal(150, r.TotalPlanifie);
            Assert.Equal(0, r.Surplus);
            Assert.Equal(50, r.Manque);
            Assert.True(r.AlerteManque);
            Assert.Equal(-50, r.Restes["M"]);
            Assert.Equal(100, r.Restes["L"]);   // intact, et pourtant non couvert
            Assert.True(r.EstCoherent);
        }

        [Fact]
        public void PlanVide_EstSurCommande_AlerteManque()
        {
            var r = MoteurCoupe.Calculer([new Gabarit("M", 5)], []);

            Assert.Equal(5, r.QuantiteCommandee);
            Assert.Equal(0, r.TotalPlanifie);
            Assert.Equal(5, r.Manque);
            Assert.True(r.AlerteManque);
        }
    }
}
