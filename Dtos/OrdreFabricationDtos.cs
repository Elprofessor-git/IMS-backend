using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos
{
    // ── OrdreFabrication ──
    public class OrdreFabricationDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string NumeroOF { get; set; } = string.Empty;
        public int? ChaineProductionId { get; set; }
        public string? ChaineProductionNom { get; set; }
        public DateTime DateCreation { get; set; }
        public string? Notes { get; set; }
        /// <summary>Total pièces de la répartition par taille (toutes lignes).</summary>
        public int TotalPieces { get; set; }
        /// <summary>Nombre de lignes de répartition.</summary>
        public int NombreLignesTailles { get; set; }
        /// <summary>Nombre d'étiquettes enregistrées pour cet OF.</summary>
        public int NombreEtiquettes { get; set; }
    }

    public class OrdreFabricationDetailDto : OrdreFabricationDto
    {
        public List<OrdreFabricationTailleDto> Tailles { get; set; } = new();
        public List<OrdreFabricationEtiquetteDto> Etiquettes { get; set; } = new();
        /// <summary>Matelas rattachés via les coupes (LotCoupe.OrdreFabricationId).</summary>
        public List<OrdreFabricationMatelasDto> Matelas { get; set; } = new();
    }

    public class CreateOrdreFabricationDto
    {
        public int CommandeId { get; set; }

        [Required]
        [StringLength(50)]
        public string NumeroOF { get; set; } = string.Empty;

        public int? ChaineProductionId { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class UpdateOrdreFabricationDto
    {
        [StringLength(50)]
        public string? NumeroOF { get; set; }

        public int? ChaineProductionId { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── OrdreFabricationTailleLigne ──
    public class OrdreFabricationTailleDto
    {
        public int Id { get; set; }
        public int OrdreFabricationId { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int Quantite { get; set; }
    }

    public class SaisieOrdreFabricationTailleDto
    {
        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int Quantite { get; set; }
    }

    // ── OrdreFabricationEtiquette ──
    public class OrdreFabricationEtiquetteDto
    {
        public int Id { get; set; }
        public int OrdreFabricationId { get; set; }
        public int? FournitureCommandeLigneId { get; set; }
        public string? FournitureDesignation { get; set; }
        public string? Taille { get; set; }
        public int QuantiteEtiquettes { get; set; }
        public DateTime DateImpression { get; set; }
        public string? EffectuePar { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateOrdreFabricationEtiquetteDto
    {
        public int? FournitureCommandeLigneId { get; set; }

        [StringLength(50)]
        public string? Taille { get; set; }

        public int QuantiteEtiquettes { get; set; } = 1;

        public DateTime? DateImpression { get; set; }

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── Matelas rattachés à un OF (via LotCoupe) ──
    public class OrdreFabricationMatelasDto
    {
        public int Id { get; set; }
        public string NumeroMatelas { get; set; } = string.Empty;
        public DateTime DateMatelas { get; set; }
        public int PiecePliage { get; set; }
        public int CoupeEstimee { get; set; }
        /// <summary>Quantité coupée rattachée à cet OF pour ce matelas (cumul LotCoupes).</summary>
        public int QuantiteCoupee { get; set; }
    }

    // ── Réponse création/édition avec avertissement de cohérence (Option B Q1) ──
    public class OrdreFabricationWriteResponse
    {
        public string Message { get; set; } = string.Empty;
        public int Id { get; set; }
        /// <summary>Warning non bloquant : Σ répartition OF ≠ Σ ConfigTailles de la commande.</summary>
        public string? AvertissementCohérence { get; set; }
    }
}