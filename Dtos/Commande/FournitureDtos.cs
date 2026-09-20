using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Commande
{
    // ── Matelas ──
    public class MatelasDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string NumeroMatelas { get; set; } = string.Empty;
        public DateTime DateMatelas { get; set; }
        public int PiecePliage { get; set; }
        public int CoupeEstimee { get; set; }
        public string? Notes { get; set; }
        public bool EstActif { get; set; }
        public int NombreCoupes { get; set; }
    }

    /// <summary>Vue globale d'un matelas (module « Coupe » transversal).</summary>
    public class MatelasGlobalDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string NumeroMatelas { get; set; } = string.Empty;
        public DateTime DateMatelas { get; set; }
        public int PiecePliage { get; set; }
        public int CoupeEstimee { get; set; }
        public int NombreCoupes { get; set; }
        public int TotalPiecesCommandees { get; set; }
        public string? Notes { get; set; }
        public bool EstActif { get; set; }
    }

    public class CreateMatelasDto
    {
        [Required]
        [StringLength(50)]
        public string NumeroMatelas { get; set; } = string.Empty;
        public DateTime? DateMatelas { get; set; }
        public int PiecePliage { get; set; }
        public int CoupeEstimee { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── ChaineProduction ──
    public class ChaineProductionDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string TypeChaine { get; set; } = string.Empty;
        public bool EstActif { get; set; }
        public int NombreEnvois { get; set; }
        public int NombreExports { get; set; }
    }

    public class CreateChaineProductionDto
    {
        [Required]
        [StringLength(100)]
        public string Nom { get; set; } = string.Empty;
        [Required]
        public string TypeChaine { get; set; } = string.Empty;
    }

    public class UpdateChaineProductionDto
    {
        [StringLength(100)]
        public string? Nom { get; set; }
        public string? TypeChaine { get; set; }
        public bool? EstActif { get; set; }
    }

    // ── FournitureCommandeLigne ──
    public class FournitureCommandeLigneDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public int ArticleId { get; set; }
        public string? ArticleDesignation { get; set; }
        public string Portee { get; set; } = string.Empty;
        public string? DesignationSpecifique { get; set; }
        public decimal QuantiteFourniture { get; set; }
        public string? Taille { get; set; }
        public string? Unite { get; set; }
        public string? Notes { get; set; }
        public decimal TotalRecu { get; set; }
        public decimal TotalEnvoye { get; set; }
    }

    public class CreateFournitureCommandeLigneDto
    {
        public int ArticleId { get; set; }
        [Required]
        public string Portee { get; set; } = string.Empty;
        [StringLength(200)]
        public string? DesignationSpecifique { get; set; }
        public decimal QuantiteFourniture { get; set; }
        [StringLength(50)]
        public string? Taille { get; set; }
        [StringLength(20)]
        public string? Unite { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class UpdateFournitureCommandeLigneDto
    {
        public int? ArticleId { get; set; }
        public string? Portee { get; set; }
        [StringLength(200)]
        public string? DesignationSpecifique { get; set; }
        public decimal? QuantiteFourniture { get; set; }
        [StringLength(50)]
        public string? Taille { get; set; }
        [StringLength(20)]
        public string? Unite { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── ReceptionFourniture ──
    public class ReceptionFournitureDto
    {
        public int Id { get; set; }
        public int CommandeFournitureLigneId { get; set; }
        public int CommandeId { get; set; }
        public int ArticleId { get; set; }
        public string? ArticleDesignation { get; set; }
        public string? Taille { get; set; }
        public decimal QuantiteRecue { get; set; }
        public DateTime DateReception { get; set; }
        public string? EffectuePar { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateReceptionFournitureDto
    {
        public int CommandeFournitureLigneId { get; set; }
        public decimal QuantiteRecue { get; set; }
        public DateTime? DateReception { get; set; }
        [StringLength(100)]
        public string? EffectuePar { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── EnvoiFourniture ──
    public class EnvoiFournitureDto
    {
        public int Id { get; set; }
        public int CommandeFournitureLigneId { get; set; }
        public int CommandeId { get; set; }
        public int ArticleId { get; set; }
        public string? ArticleDesignation { get; set; }
        public string? Taille { get; set; }
        public int? ChaineProductionId { get; set; }
        public string? ChaineProductionNom { get; set; }
        public decimal QuantiteEnvoyee { get; set; }
        public DateTime DateEnvoi { get; set; }
        public string? EffectuePar { get; set; }
        public bool ForcerDepassement { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateEnvoiFournitureDto
    {
        public int CommandeFournitureLigneId { get; set; }
        public int? ChaineProductionId { get; set; }
        public decimal QuantiteEnvoyee { get; set; }
        public DateTime? DateEnvoi { get; set; }
        [StringLength(100)]
        public string? EffectuePar { get; set; }
        public bool ForcerDepassement { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── RapportFournitures ──
    public class RapportFournitureArticleDto
    {
        public int CommandeFournitureLigneId { get; set; }
        public int ArticleId { get; set; }
        public string ArticleDesignation { get; set; } = string.Empty;
        public string? Taille { get; set; }
        public string Portee { get; set; } = string.Empty;
        public decimal QuantiteParPiece { get; set; }
        public string? Unite { get; set; }
        public decimal BesoinCalcule { get; set; }
        public decimal TotalRecu { get; set; }
        public decimal TotalEnvoye { get; set; }
        public decimal Ecart { get; set; }
    }

    public class RapportFournitureChaineDto
    {
        public int? ChaineProductionId { get; set; }
        public string? ChaineProductionNom { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int PiècesExportees { get; set; }
        public decimal FournituresAttendues { get; set; }
        public decimal FournituresEnvoyees { get; set; }
        public decimal Ecart { get; set; }
    }

    public class RapportFournituresDto
    {
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public int TotalMatelas { get; set; }
        public int TotalPiecesCoupees { get; set; }
        public List<RapportFournitureArticleDto> Articles { get; set; } = new();
        public List<RapportFournitureChaineDto> ParChaine { get; set; } = new();
    }

    // ── CommandeClient lookup DTO (pour les matelas rattachés à une commande) ──
    public class CommandeMatelasDto
    {
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public List<MatelasDto> Matelas { get; set; } = new();
    }
}
