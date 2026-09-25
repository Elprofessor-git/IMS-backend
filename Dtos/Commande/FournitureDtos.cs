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
        public decimal? Longueur { get; set; }
        public decimal? Laize { get; set; }
        public string? Notes { get; set; }
        public bool EstActif { get; set; }
        public int NombreCoupes { get; set; }
    }

    /// <summary>Vue globale d'un matelas (module « Coupe » transversal).</summary>
    public class MatelasGlobalDto
    {
        public int Id { get; set; }
        public int? CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string NumeroMatelas { get; set; } = string.Empty;
        public DateTime DateMatelas { get; set; }
        public int PiecePliage { get; set; }
        public int CoupeEstimee { get; set; }
        public decimal? Longueur { get; set; }
        public decimal? Laize { get; set; }
        public int NombreCoupes { get; set; }
        public int TotalPiecesCommandees { get; set; }
        /// <summary>Total théorique du plan = Σ (Occurrences × PiecePliage).</summary>
        public int TotalPlanTheorique { get; set; }
        public string? Notes { get; set; }
        public bool EstActif { get; set; }
        /// <summary>
        /// Ordre de coupe calculé (vue, sans table de planning) : rang du matelas
        /// dans la séquence de coupe triée par (DateMatelas, NumeroMatelas) croissant.
        /// Le matelas d'ordre 1 est le premier à découper.
        /// </summary>
        public int OrdreDeCoupe { get; set; }
    }

    /// <summary>Mise à jour d'un matelas (PUT /api/Matelas/{id}).</summary>
    public class UpdateMatelasDto
    {
        public int? CommandeId { get; set; }
        [StringLength(50)]
        public string? NumeroMatelas { get; set; }
        public DateTime? DateMatelas { get; set; }
        public int? PiecePliage { get; set; }
        public int? CoupeEstimee { get; set; }
        public decimal? Longueur { get; set; }
        public decimal? Laize { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
        public bool? EstActif { get; set; }
    }

    /// <summary>Totaux globaux du module Coupe (toutes commandes).</summary>
    public class MatelasStatsDto
    {
        public int TotalMatelas { get; set; }
        public int TotalMatelasActifs { get; set; }
        public int TotalPiecesCommandees { get; set; }
        public int TotalPiecesCoupees { get; set; }
        public int TotalPiecesExportees { get; set; }
    }

    /// <summary>
    /// Journal du jour du module Coupe : coupes enregistrées aujourd'hui, toutes
    /// commandes confondues (tableau de bord global, lecture seule).
    /// </summary>
    public class JournalCoupeDto
    {
        public DateTime Date { get; set; }
        public int NombreLignes { get; set; }
        public int TotalQuantite { get; set; }
        public List<JournalCoupeLigneDto> Lignes { get; set; } = new();
    }

    public class JournalCoupeLigneDto
    {
        public int Id { get; set; }
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string Taille { get; set; } = string.Empty;
        public int QuantiteCoupee { get; set; }
        public DateTime DateCoupe { get; set; }
        public string? EffectuePar { get; set; }
        public bool ForcerDepassement { get; set; }
        public int? MatelasId { get; set; }
        public string? MatelasNumero { get; set; }
    }

    /// <summary>
    /// Tableau de bord d'avancement du module Coupe, agrégé PAR COMMANDE.
    /// Point de départ : les commandes (et non les matelas) — une commande sans
    /// aucun matelas y figure avec planifié = 0 et reste à planifier = demandé.
    /// Agrégats calculés en mémoire à partir de jointures SQL : aucun champ
    /// calculé n'est stocké, donc aucune migration.
    /// </summary>
    public class CoupeDashboardDto
    {
        public DateTime Date { get; set; }
        /// <summary>Commandes retenues (annulées exclues).</summary>
        public int NombreCommandes { get; set; }
        /// <summary>Σ ConfigTaille.Quantite des commandes retenues.</summary>
        public int PiecesDemandees { get; set; }
        /// <summary>Σ (Occurrences × PiecePliage) des plans de leurs matelas.</summary>
        public int PiecesPlanifiees { get; set; }
        /// <summary>Σ LotCoupe.QuantiteCoupee.</summary>
        public int PiecesCoupees { get; set; }
        public int ResteAPlanifier { get; set; }
        public int ResteACouper { get; set; }
        public List<CoupeDashboardCommandeDto> Commandes { get; set; } = new();
    }

    public class CoupeDashboardCommandeDto
    {
        public int CommandeId { get; set; }
        public string NumeroCommande { get; set; } = string.Empty;
        public string? TitreCommande { get; set; }
        public string? ClientNom { get; set; }
        public string? PlateformeNom { get; set; }
        public string Statut { get; set; } = string.Empty;
        public DateTime DateCommande { get; set; }
        public int NombreMatelas { get; set; }
        public int PiecesDemandees { get; set; }
        public int PiecesPlanifiees { get; set; }
        public int PiecesCoupees { get; set; }
        /// <summary>Commandes non annulées uniquement (une commande annulée n'a rien à planifier).</summary>
        public int ResteAPlanifier { get; set; }
        public int ResteACouper { get; set; }
        /// <summary>Coupes orphelines (sans matelas rattaché) — hors calcul des restes.</summary>
        public int CoupesSansMatelas { get; set; }
        public int Avancement { get; set; }
    }

    public class CreateMatelasDto
    {
        [Required]
        public int CommandeId { get; set; }
        [Required]
        [StringLength(50)]
        public string NumeroMatelas { get; set; } = string.Empty;
        public DateTime? DateMatelas { get; set; }
        public int PiecePliage { get; set; }
        public int CoupeEstimee { get; set; }
        public decimal? Longueur { get; set; }
        public decimal? Laize { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── Plan de coupe (marker par matelas) — L1 ──
    public class PlanDeCoupeLigneDto
    {
        public int Id { get; set; }
        public int MatelasId { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public string? Notes { get; set; }
        /// <summary>Quantité théorique = Occurrences × Matelas.PiecePliage (calculée).</summary>
        public int Theorique { get; set; }
    }

    public class CreatePlanDeCoupeLigneDto
    {
        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int Occurrences { get; set; } = 1;
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class UpdatePlanDeCoupeLigneDto
    {
        [StringLength(50)]
        public string? Taille { get; set; }
        [Range(1, int.MaxValue)]
        public int? Occurrences { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── ChaineProduction ──
    public class ChaineProductionDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string TypeChaine { get; set; } = string.Empty;
        public bool EstSousTraitant { get; set; } = true;
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
        public bool? EstSousTraitant { get; set; }
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
