namespace Backend_Gestion_Magasin_API.Models
{
    // Mode de règlement d'un achat (distinct de ConditionsPaiement qui est un délai/terme libre).
    // Stocké en string en base (voir HasConversion<string> dans ApplicationDbContext).
    public enum TypePaiement
    {
        Especes,
        Cheque,
        Virement,
        Autre
    }
}
