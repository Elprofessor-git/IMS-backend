namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    // ───────────────────────────── Ordre de coupe (C2) ─────────────────────────────
    // Un ordre par couple (commande, modèle, couleur). TOUTE cellule est stockée telle
    // que saisie : les restes, le résumé et les grandeurs de matières sont recalculés à
    // la lecture et ne sont jamais persistés.

    /// <summary>Création d'un ordre de coupe (en-tête seul, sans préremplissage).</summary>
    public class CreerOrdreCoupeDto
    {
        public int CommandeId { get; set; }
        public string Modele { get; set; } = string.Empty;
        public string Couleur { get; set; } = string.Empty;
        public int? ChaineProductionId { get; set; }
        /// <summary>NULL = marge par défaut de la commande.</summary>
        public decimal? MargeSecurite { get; set; }
        public string? ReferenceOF { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>En-tête d'un ordre de coupe (remplacement complet des champs).</summary>
    public class ModifierOrdreCoupeDto
    {
        public string Modele { get; set; } = string.Empty;
        public string Couleur { get; set; } = string.Empty;
        public int? ChaineProductionId { get; set; }
        /// <summary>NULL = retour à la marge par défaut de la commande.</summary>
        public decimal? MargeSecurite { get; set; }
        public string? ReferenceOF { get; set; }
        public string? Notes { get; set; }
    }

    public class CreerTailleDto
    {
        public string Libelle { get; set; } = string.Empty;
        public int? QuantiteDemandee { get; set; }
        public decimal? QuantiteAvecMarge { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Colonne de taille (remplacement complet : NULL = cellule vidée).</summary>
    public class ModifierTailleDto
    {
        public string Libelle { get; set; } = string.Empty;
        public int? QuantiteDemandee { get; set; }
        public decimal? QuantiteAvecMarge { get; set; }
        public string? Notes { get; set; }
    }

    public class OccurrenceDto
    {
        public string Taille { get; set; } = string.Empty;
        public int Occurrences { get; set; }
    }

    public class CreerPlanDto
    {
        public string? Libelle { get; set; }
        public int Plis { get; set; }
        /// <summary>Matelas suivi rattaché au plan (optionnel — c'est lui qui porte le verrou).</summary>
        public int? MatelasId { get; set; }
        public List<OccurrenceDto> Occurrences { get; set; } = new();
    }

    /// <summary>Plan (remplacement complet : la liste d'occurrences écrase l'existant).</summary>
    public class ModifierPlanDto
    {
        public string? Libelle { get; set; }
        public int Plis { get; set; }
        public int? MatelasId { get; set; }
        public List<OccurrenceDto> Occurrences { get; set; } = new();
    }

    public class CreerMatiereDto
    {
        public string Designation { get; set; } = string.Empty;
        public decimal? Laize { get; set; }
        public decimal ConsoClient { get; set; }
        public decimal? ConsoReelle { get; set; }
        public decimal RetraitPourcentage { get; set; }
        public decimal MetresRecus { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Matière (remplacement complet des champs).</summary>
    public class ModifierMatiereDto
    {
        public string Designation { get; set; } = string.Empty;
        public decimal? Laize { get; set; }
        public decimal ConsoClient { get; set; }
        public decimal? ConsoReelle { get; set; }
        public decimal RetraitPourcentage { get; set; }
        public decimal MetresRecus { get; set; }
        public string? Notes { get; set; }
    }

    // ── Réponses (vue calculée) ──

    /// <summary>Colonne de taille, avec ses cellules de quantité telles que saisies.</summary>
    public class OrdreCoupeTailleDto
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string Libelle { get; set; } = string.Empty;
        /// <summary>NULL = cellule jamais remplie.</summary>
        public int? QuantiteDemandee { get; set; }
        public decimal? QuantiteAvecMarge { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Reste d'une taille après une passe (calculé, jamais stocké).</summary>
    public class OrdreCoupeResteDto
    {
        public string Taille { get; set; } = string.Empty;
        /// <summary>Négatif = surplus : la valeur est conservée et affichée en rouge.</summary>
        public int Valeur { get; set; }
    }

    public class OrdreCoupePlanDto
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string? Libelle { get; set; }
        public int Plis { get; set; }
        public int? MatelasId { get; set; }
        public string? MatelasNumero { get; set; }
        /// <summary>true si le matelas rattaché porte déjà des coupes : plan figé (409).</summary>
        public bool Verrouille { get; set; }
        public List<OccurrenceDto> Occurrences { get; set; } = new();
        /// <summary>Restes par taille APRÈS cette passe.</summary>
        public List<OrdreCoupeResteDto> Restes { get; set; } = new();
    }

    /// <summary>Matière, avec ses grandeurs calculées à la lecture.</summary>
    public class OrdreCoupeMatiereDto
    {
        public int Id { get; set; }
        public string Designation { get; set; } = string.Empty;
        public decimal? Laize { get; set; }
        public decimal ConsoClient { get; set; }
        /// <summary>Informatif : n'entre jamais dans le calcul.</summary>
        public decimal? ConsoReelle { get; set; }
        /// <summary>Écart = ConsoReelle − ConsoClient (NULL si la conso réelle est vide).</summary>
        public decimal? EcartConso { get; set; }
        public decimal RetraitPourcentage { get; set; }
        public decimal MetresRecus { get; set; }
        public string? Notes { get; set; }
        /// <summary>Utilisés = total planifié de l'ordre × ConsoClient.</summary>
        public decimal Utilises { get; set; }
        /// <summary>Retrait = RetraitPourcentage % × MetresRecus.</summary>
        public decimal Retrait { get; set; }
        /// <summary>Stock restant = reçus − utilisés − retrait.</summary>
        public decimal StockRestant { get; set; }
        /// <summary>true si le stock restant est négatif (alerte rouge).</summary>
        public bool Alerte { get; set; }
    }

    public class OrdreCoupeDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string Modele { get; set; } = string.Empty;
        public string Couleur { get; set; } = string.Empty;
        public int? ChaineProductionId { get; set; }
        public string? ChaineProductionNom { get; set; }
        /// <summary>Marge retenue : celle de l'ordre, ou celle de la commande si vide.</summary>
        public decimal MargeSecurite { get; set; }
        /// <summary>true si la marge vient de l'ordre (et non de la commande).</summary>
        public bool MargePersonnalisee { get; set; }
        public string? ReferenceOF { get; set; }
        public string? Notes { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime? DateMiseAJour { get; set; }
        /// <summary>Un de ses plans est figé : des coupes réelles existent.</summary>
        public bool Verrouille { get; set; }

        // Résumé — tout recalculé, rien de stocké.
        public long TotalCommande { get; set; }
        public long TotalPlanifie { get; set; }
        public long Surplus { get; set; }
        public long Manque { get; set; }
        public bool AlerteManque { get; set; }

        public List<OrdreCoupeTailleDto> Tailles { get; set; } = new();
        public List<OrdreCoupePlanDto> Plans { get; set; } = new();
        public List<OrdreCoupeMatiereDto> Matieres { get; set; } = new();
    }

    /// <summary>Ligne de la liste des ordres de coupe d'une commande.</summary>
    public class OrdreCoupeListeDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string Modele { get; set; } = string.Empty;
        public string Couleur { get; set; } = string.Empty;
        public string? ChaineProductionNom { get; set; }
        public int NbTailles { get; set; }
        public int NbPlans { get; set; }
        public int NbMatieres { get; set; }
        public long TotalCommande { get; set; }
        public long TotalPlanifie { get; set; }
        public bool AlerteManque { get; set; }
        public bool Verrouille { get; set; }
        public DateTime DateCreation { get; set; }
    }

    /// <summary>Bilan du préremplissage explicite.</summary>
    public class PreremplissageDto
    {
        public int TaillesAjoutees { get; set; }
        public int TaillesIgnorees { get; set; }
        public int CellulesRemplies { get; set; }
        public int CellulesConservees { get; set; }
        public decimal MargeSecurite { get; set; }
        public string? Message { get; set; }
    }
}
