using System.ComponentModel.DataAnnotations;

namespace Backend_Gestion_Magasin_API.Dtos.Qualite
{
    // ── Création ControleQualite (réception + contrôle en une saisie) ──
    public class CreateControleQualiteDto
    {
        /// <summary>Rattachement provisoire à l'OF (4.1) — sera OrdreFabricationEtapeId.</summary>
        public int OrdreFabricationId { get; set; }

        /// <summary>Portée interne/sous-traitant : chaîne contrôlée.</summary>
        public int? ChaineProductionId { get; set; }

        [Required]
        public string? TypeControle { get; set; }  // "Interne" | "RetourSousTraitant"

        [Required]
        [StringLength(50)]
        public string Taille { get; set; } = string.Empty;

        public int QuantiteControlee { get; set; }
        public int QuantiteAcceptee { get; set; }
        public int QuantiteRetouche { get; set; }
        public int QuantiteRebut { get; set; }

        /// <summary>Tour N+1 : contrôle retour de l'envoi qui re-facture le re-contrôle.</summary>
        public int? ControleParentId { get; set; }

        /// <summary>Tour N+1 : envoi re-contrôlé.</summary>
        public int? EnvoiRetoucheId { get; set; }

        public DateTime? DateControle { get; set; }

        [StringLength(100)]
        public string? EffectuePar { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public List<CreateControleQualiteDefautLigneDto> Defauts { get; set; } = new();
    }

    public class CreateControleQualiteDefautLigneDto
    {
        public int DefautCodeId { get; set; }
        public int Quantite { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── Renvoi retouche ──
    public class CreateEnvoiRetoucheDto
    {
        public int ControleQualiteId { get; set; }
        public int ChaineProductionId { get; set; }
        public int QuantiteRenvoyee { get; set; }
        public DateTime? DateEnvoi { get; set; }
        [StringLength(100)]
        public string? EffectuePar { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    // ── Défaut ──
    public class CreateDefautCodeDto
    {
        [Required]
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;
        [Required]
        [StringLength(150)]
        public string Libelle { get; set; } = string.Empty;
        public bool EstActif { get; set; } = true;
    }

    public class UpdateDefautCodeDto
    {
        [StringLength(30)]
        public string? Code { get; set; }
        [StringLength(150)]
        public string? Libelle { get; set; }
        public bool? EstActif { get; set; }
    }

    public class DefautCodeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public bool EstActif { get; set; }
    }

    // ── Lecture / board ──
    public class ControleQualiteDto
    {
        public int Id { get; set; }
        public int? OrdreFabricationId { get; set; }
        public string? NumeroCommande { get; set; }
        public int? ChaineProductionId { get; set; }
        public string? ChaineNom { get; set; }
        public string TypeControle { get; set; } = string.Empty;
        public int? ControleParentId { get; set; }
        public int? EnvoiRetoucheId { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int QuantiteControlee { get; set; }
        public int QuantiteAcceptee { get; set; }
        public int QuantiteRetouche { get; set; }
        public int QuantiteRebut { get; set; }
        public DateTime DateControle { get; set; }
        public string? EffectuePar { get; set; }
        public string? Notes { get; set; }
        public List<ControleQualiteDefautLigneDto> Defauts { get; set; } = new();
    }

    public class ControleQualiteDefautLigneDto
    {
        public int Id { get; set; }
        public int DefautCodeId { get; set; }
        public string? DefautCode { get; set; }
        public string? DefautLibelle { get; set; }
        public int Quantite { get; set; }
        public string? Notes { get; set; }
    }

    public class EnvoiRetoucheDto
    {
        public int Id { get; set; }
        public int ControleQualiteId { get; set; }
        public int ChaineProductionId { get; set; }
        public string? ChaineNom { get; set; }
        public int QuantiteRenvoyee { get; set; }
        public DateTime DateEnvoi { get; set; }
        public string? EffectuePar { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Stock = quantité restant à traiter pour le triplet (OF, Chaîne, Taille),
    /// et la référence disponible pour un nouveau contrôle :
    ///   tour 1   : R1  = Q(export triplet) − Σ contrôlés tour 1 (TypeControle=Interne, sans parent)
    ///   tour N+1 : RN+1 = QuantiteRenvoyee(envoi) − Σ recontrôlés fils(envoi).
    /// Identité comptable : Q = ΣA + ΣB + EnCours(t). Soldé dès EnCours = 0.
    /// </summary>
    public class ReferenceQualiteDto
    {
        public int TripletOfId { get; set; }
        public int? TripletChaineId { get; set; }
        public string TripletTaille { get; set; } = string.Empty;
        public int QuantiteExportee { get; set; }
        public int QuantiteControlee { get; set; }
        public int QuantiteAcceptee { get; set; }
        public int QuantiteRetouche { get; set; }
        public int QuantiteRebut { get; set; }
        public int EnCours { get; set; }
        public int ReferenceDisponible { get; set; }
        public bool EstSolde { get; set; }
    }

    /// <summary>Fragment du cycle d'un triplet, pour l'écran Réception + Contrôle.</summary>
    public class CycleQualiteDto
    {
        public int? OrdreFabricationId { get; set; }
        public string? NumeroCommande { get; set; }
        public int? ChaineProductionId { get; set; }
        public string? ChaineNom { get; set; }
        public string Taille { get; set; } = string.Empty;
        public int QuantiteExportee { get; set; }
        public int QuantiteControleeTotale { get; set; }
        public int QuantiteAccepteeTotale { get; set; }
        public int QuantiteRetoucheTotale { get; set; }
        public int QuantiteRebutTotale { get; set; }
        public int EnCours { get; set; }
        public bool EstSolde { get; set; }
        public List<ControleQualiteDto> Controles { get; set; } = new();
        public List<EnvoiRetoucheDto> Envois { get; set; } = new();
    }
}