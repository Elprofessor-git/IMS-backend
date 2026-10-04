namespace Backend_Gestion_Magasin_API.Tests
{
    /// <summary>
    /// Forme du fixture backend.Tests/Fixtures/ordre-coupe-2026.json, extrait de
    /// « ORDRE DE COUPE-2026.xlsx ». Volontairement minimal : on ne garde que ce
    /// que le moteur consomme, plus les valeurs d'en-tete pour le rapport
    /// d'ecart (jamais comparees au moteur).
    /// </summary>
    public sealed class Corpus
    {
        public string source { get; set; } = string.Empty;
        public string regle { get; set; } = string.Empty;
        public string totalPlanifie { get; set; } = string.Empty;
        public string avertissement { get; set; } = string.Empty;
        public List<Feuille> feuilles { get; set; } = [];
    }

    public sealed class Feuille
    {
        public string feuille { get; set; } = string.Empty;
        public string modele { get; set; } = string.Empty;
        public string couleur { get; set; } = string.Empty;
        public int qtCommande { get; set; }
        public double? consoTissu { get; set; }
        public double? tisRecu { get; set; }
        public List<string> tailles { get; set; } = [];
        public Dictionary<string, int> quantites { get; set; } = [];
        public List<Passe> passes { get; set; } = [];
        // Le nom porte le tiret bas comme la cle JSON : `_attendu` signale une valeur
        // RECALCULEE a l'extraction, pas une valeur lue dans le classeur.
        public Attendu _attendu { get; set; } = new();
        public EnTeteClasseur enTeteClasseur { get; set; } = new();
    }

    public sealed class Passe
    {
        public int numero { get; set; }
        public int plis { get; set; }
        public Dictionary<string, int> occurrences { get; set; } = [];
    }

    /// <summary>Valeurs recalculees a l'extraction, pour detecter une formule qui bouge.</summary>
    public sealed class Attendu
    {
        public long metrage { get; set; }
        public long surplus { get; set; }
        public long manque { get; set; }
        public Dictionary<string, int> restes { get; set; } = [];
    }

    /// <summary>
    /// Valeurs relues dans le classeur. INFORMATIFS : jamais assertes contre le
    /// moteur. Seul le rapport d'ecart les cite, pour que la divergence d'OVITA reste
    /// visible sans etre forcee.
    /// </summary>
    public sealed class EnTeteClasseur
    {
        public long? qtCoupe { get; set; }
        /// <summary>Cellule « QT » saisie a la main quand elle existe.</summary>
        public long? qtSaisi { get; set; }
        public long? tissuQt { get; set; }
        public double? tissuConso { get; set; }
        public double? tissuRestant { get; set; }
    }
}
