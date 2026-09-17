using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.Identity;

namespace Backend_Gestion_Magasin_API.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbContext is configured in Program.cs, no need for OnConfiguring

        // DbSets pour tous les modèles
        public DbSet<Plateforme> Plateformes { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Fournisseur> Fournisseurs { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<MouvementStock> MouvementsStock { get; set; }
        public DbSet<CommandeClient> CommandesClients { get; set; }
        public DbSet<BesoinCommande> BesoinsCommandes { get; set; }
        public DbSet<TacheProduction> TachesProduction { get; set; }
        public DbSet<Achat> Achats { get; set; }
        public DbSet<LigneAchat> LignesAchat { get; set; }
        public DbSet<Importation> Importations { get; set; }
        public DbSet<LigneImportation> LignesImportation { get; set; }
        public DbSet<Role> AppRoles { get; set; }
        public DbSet<Tache> Taches { get; set; }
        public DbSet<FournisseurClient> FournisseurClients { get; set; }
        public DbSet<DocumentJoint> DocumentsJoints { get; set; }
        public DbSet<DocumentImportation> DocumentsImportation { get; set; }
        public DbSet<ConfigTaille> ConfigTailles { get; set; }
        public DbSet<BomLigne> BomLignes { get; set; }
        public DbSet<ResultatCalcul> ResultatsCalcul { get; set; }
        public DbSet<ModeleBom> ModeleBoms { get; set; }
        public DbSet<FournitureBom> FournituresBom { get; set; }
        public DbSet<HistoriquePrixArticle> HistoriquesPrixArticles { get; set; }
        public DbSet<GroupeCommande> GroupesCommandes { get; set; }
        public DbSet<GroupeCommandeCommande> GroupeCommandeCommandes { get; set; }
        public DbSet<LotCoupe> LotCoupes { get; set; }
        public DbSet<LotExport> LotExports { get; set; }
        public DbSet<Matelas> Matelas { get; set; }
        public DbSet<ChaineProduction> ChainesProduction { get; set; }
        public DbSet<FournitureCommandeLigne> FournitureCommandesLignes { get; set; }
        public DbSet<ReceptionFourniture> ReceptionsFourniture { get; set; }
        public DbSet<EnvoiFourniture> EnvoisFourniture { get; set; }
        public DbSet<Facture> Factures { get; set; }
        public DbSet<FactureCommandeLigne> FactureCommandesLignes { get; set; }
        public DbSet<OrdreFabrication> OrdresFabrication { get; set; }
        public DbSet<OrdreFabricationTailleLigne> OrdresFabricationTailles { get; set; }
        public DbSet<OrdreFabricationEtiquette> OrdresFabricationEtiquettes { get; set; }
        public DbSet<Devise> Devises { get; set; }
        public DbSet<TauxChange> TauxChanges { get; set; }
        public DbSet<Machine> Machines { get; set; }
        public DbSet<InterventionMachine> InterventionsMachines { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuration des relations et contraintes

            // CommandeClient -> ConfigTaille (One-to-Many)
            modelBuilder.Entity<ConfigTaille>()
                .HasOne(ct => ct.Commande)
                .WithMany(c => c.ConfigTailles)
                .HasForeignKey(ct => ct.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            // CommandeClient -> BomLigne (One-to-Many)
            modelBuilder.Entity<BomLigne>()
                .HasOne(b => b.Commande)
                .WithMany(c => c.BomLignes)
                .HasForeignKey(b => b.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BomLigne>()
                .HasOne(b => b.Article)
                .WithMany()
                .HasForeignKey(b => b.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            // CommandeClient -> FactureCommandeLigne (One-to-Many)
            modelBuilder.Entity<FactureCommandeLigne>()
                .HasOne(fcl => fcl.Facture)
                .WithMany(f => f.Lignes)
                .HasForeignKey(fcl => fcl.FactureId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FactureCommandeLigne>()
                .HasOne(fcl => fcl.Commande)
                .WithMany(c => c.FacturesLignes)
                .HasForeignKey(fcl => fcl.CommandeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Facture -> Client (One-to-Many)
            modelBuilder.Entity<Facture>()
                .HasOne(f => f.Client)
                .WithMany()
                .HasForeignKey(f => f.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Facture>()
                .HasIndex(f => f.NumeroFacture)
                .IsUnique();

            // CommandeClient -> LotCoupe (One-to-Many)
            modelBuilder.Entity<LotCoupe>()
                .HasOne(lc => lc.Commande)
                .WithMany(c => c.LotCoupes)
                .HasForeignKey(lc => lc.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            // CommandeClient -> LotExport (One-to-Many)
            modelBuilder.Entity<LotExport>()
                .HasOne(le => le.Commande)
                .WithMany(c => c.LotExports)
                .HasForeignKey(le => le.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            // CommandeClient -> ResultatCalcul (One-to-Many)
            modelBuilder.Entity<ResultatCalcul>()
                .HasOne(r => r.Commande)
                .WithMany(c => c.ResultatsCalcul)
                .HasForeignKey(r => r.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ResultatCalcul>()
                .HasOne(r => r.Article)
                .WithMany()
                .HasForeignKey(r => r.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Précisions décimales BomLigne / ResultatCalcul
            modelBuilder.Entity<BomLigne>()
                .Property(b => b.QuantiteParPiece)
                .HasPrecision(18, 4);

            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.BesoinBrut).HasPrecision(18, 4);
            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.MargeAppliquee).HasPrecision(5, 2);
            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.BesoinFinal).HasPrecision(18, 4);
            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.QteAchat).HasPrecision(18, 4);
            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.QteImport).HasPrecision(18, 4);
            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.QteStockReserve).HasPrecision(18, 4);
            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.QteDisponible).HasPrecision(18, 4);
            modelBuilder.Entity<ResultatCalcul>()
                .Property(r => r.Manque).HasPrecision(18, 4);

            // Plateforme -> Client (One-to-Many)
            modelBuilder.Entity<Client>()
                .HasOne(c => c.Plateforme)
                .WithMany(p => p.Clients)
                .HasForeignKey(c => c.PlateformeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Client -> CommandeClient (One-to-Many)
            modelBuilder.Entity<CommandeClient>()
                .HasOne(cc => cc.Client)
                .WithMany(c => c.Commandes)
                .HasForeignKey(cc => cc.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // CommandeClient -> BesoinCommande (One-to-Many)
            modelBuilder.Entity<BesoinCommande>()
                .HasOne(bc => bc.CommandeClient)
                .WithMany(cc => cc.Besoins)
                .HasForeignKey(bc => bc.CommandeClientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Article -> BesoinCommande (One-to-Many)
            modelBuilder.Entity<BesoinCommande>()
                .HasOne(bc => bc.Article)
                .WithMany(a => a.BesoinsCommande)
                .HasForeignKey(bc => bc.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            // CommandeClient -> TacheProduction (One-to-Many)
            modelBuilder.Entity<TacheProduction>()
                .HasOne(tp => tp.CommandeClient)
                .WithMany(cc => cc.Taches)
                .HasForeignKey(tp => tp.CommandeClientId)
                .OnDelete(DeleteBehavior.SetNull);

            // Article -> Stock (One-to-Many)
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Article)
                .WithMany(a => a.Stocks)
                .HasForeignKey(s => s.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Client -> Stock (scope optionnel)
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Client)
                .WithMany()
                .HasForeignKey(s => s.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Plateforme -> Stock (scope optionnel)
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Plateforme)
                .WithMany()
                .HasForeignKey(s => s.PlateformeId)
                .OnDelete(DeleteBehavior.Restrict);

            // LigneAchat -> Stock (traçabilité réception — Fonctionnalité 18, optionnel)
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.LigneAchat)
                .WithMany()
                .HasForeignKey(s => s.LigneAchatId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // LigneImportation -> Stock (traçabilité réception — Fonctionnalité 18, optionnel)
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.LigneImportation)
                .WithMany()
                .HasForeignKey(s => s.LigneImportationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Stock -> MouvementStock (One-to-Many)
            modelBuilder.Entity<MouvementStock>()
                .HasOne(ms => ms.Stock)
                .WithMany(s => s.Mouvements)
                .HasForeignKey(ms => ms.StockId)
                .OnDelete(DeleteBehavior.Cascade);

            // TacheProduction -> MouvementStock (One-to-Many)
            modelBuilder.Entity<MouvementStock>()
                .HasOne(ms => ms.TacheProduction)
                .WithMany(tp => tp.MouvementsStock)
                .HasForeignKey(ms => ms.TacheProductionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Fournisseur -> Achat (One-to-Many)
            modelBuilder.Entity<Achat>()
                .HasOne(a => a.Fournisseur)
                .WithMany(f => f.Achats)
                .HasForeignKey(a => a.FournisseurId)
                .OnDelete(DeleteBehavior.Restrict);

            // CommandeClient -> Achat (One-to-Many, optionnel — scope principal pour TacheProduction)
            modelBuilder.Entity<Achat>()
                .HasOne(a => a.CommandeClient)
                .WithMany(cc => cc.Achats)
                .HasForeignKey(a => a.CommandeClientId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Achat -> LigneAchat (One-to-Many)
            modelBuilder.Entity<LigneAchat>()
                .HasOne(la => la.Achat)
                .WithMany(a => a.LignesAchat)
                .HasForeignKey(la => la.AchatId)
                .OnDelete(DeleteBehavior.Cascade);

            // Article -> LigneAchat (One-to-Many)
            modelBuilder.Entity<LigneAchat>()
                .HasOne(la => la.Article)
                .WithMany(a => a.LignesAchat)
                .HasForeignKey(la => la.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            // CommandeClient -> LigneAchat (scope Commande, optionnel)
            modelBuilder.Entity<LigneAchat>()
                .HasOne(la => la.CommandeClient)
                .WithMany()
                .HasForeignKey(la => la.CommandeClientId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Client -> LigneAchat (scope Marque, optionnel)
            modelBuilder.Entity<LigneAchat>()
                .HasOne(la => la.Client)
                .WithMany()
                .HasForeignKey(la => la.ClientId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Plateforme -> LigneAchat (scope Plateforme, optionnel)
            modelBuilder.Entity<LigneAchat>()
                .HasOne(la => la.Plateforme)
                .WithMany()
                .HasForeignKey(la => la.PlateformeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Fournisseur -> Importation (One-to-Many)
            modelBuilder.Entity<Importation>()
                .HasOne(i => i.Fournisseur)
                .WithMany(f => f.Importations)
                .HasForeignKey(i => i.FournisseurId)
                .OnDelete(DeleteBehavior.Restrict);

            // Plateforme -> Importation (source alternative au Fournisseur, optionnelle)
            modelBuilder.Entity<Importation>()
                .HasOne(i => i.Plateforme)
                .WithMany()
                .HasForeignKey(i => i.PlateformeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Importation -> DocumentImportation (One-to-Many)
            modelBuilder.Entity<DocumentImportation>()
                .HasOne(d => d.Importation)
                .WithMany(i => i.Documents)
                .HasForeignKey(d => d.ImportationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Importation -> LigneImportation (One-to-Many)
            modelBuilder.Entity<LigneImportation>()
                .HasOne(li => li.Importation)
                .WithMany(i => i.LignesImportation)
                .HasForeignKey(li => li.ImportationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Article -> LigneImportation (One-to-Many)
            modelBuilder.Entity<LigneImportation>()
                .HasOne(li => li.Article)
                .WithMany(a => a.LignesImportation)
                .HasForeignKey(li => li.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            // CommandeClient -> LigneImportation (One-to-Many, optionnel)
            modelBuilder.Entity<LigneImportation>()
                .HasOne(li => li.CommandeClient)
                .WithMany()
                .HasForeignKey(li => li.CommandeClientId)
                .OnDelete(DeleteBehavior.SetNull);

            // Client -> LigneImportation (scope Marque, optionnel)
            modelBuilder.Entity<LigneImportation>()
                .HasOne(li => li.Client)
                .WithMany()
                .HasForeignKey(li => li.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Plateforme -> LigneImportation (scope Plateforme = DESTINATION, optionnel)
            modelBuilder.Entity<LigneImportation>()
                .HasOne(li => li.Plateforme)
                .WithMany()
                .HasForeignKey(li => li.PlateformeId)
                .OnDelete(DeleteBehavior.Restrict);


            // Role -> ApplicationUser (One-to-Many)
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Utilisateurs)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configuration des propriétés décimales pour PostgreSQL
            modelBuilder.Entity<Stock>()
                .Property(s => s.Quantite)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Stock>()
                .Property(s => s.QuantiteReservee)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Stock>()
                .Property(s => s.PrixUnitaire)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Stock>()
                .Property(s => s.PrixUnitaireTND)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteUnitaire)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteTotale)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteCouverte)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteAchatsLocaux)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteStockImporte)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteStockLibre)
                .HasPrecision(18, 4);

            modelBuilder.Entity<CommandeClient>()
                .Property(cc => cc.MontantTotal)
                .HasPrecision(18, 4);

            modelBuilder.Entity<CommandeClient>()
                .Property(cc => cc.PourcentageRessourcesCouvertes)
                .HasPrecision(5, 2);

            modelBuilder.Entity<CommandeClient>()
                .Property(cc => cc.MargeSecuriteDefaut)
                .HasPrecision(5, 2);

            modelBuilder.Entity<TacheProduction>()
                .Property(tp => tp.PourcentageAvancement)
                .HasPrecision(5, 2);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.Quantite)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.QuantiteRecue)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.PrixUnitaire)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.MontantLigne)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.MontantLigneTND)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Achat>()
                .Property(a => a.MontantTotal)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Achat>()
                .Property(a => a.MontantTotalTND)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.Quantite)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.QuantiteRecue)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.PrixUnitaire)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.MontantLigne)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.MontantLigneTND)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Importation>()
                .Property(i => i.MontantTotal)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Importation>()
                .Property(i => i.MontantTotalTND)
                .HasPrecision(18, 4);

            modelBuilder.Entity<MouvementStock>()
                .Property(ms => ms.Quantite)
                .HasPrecision(18, 4);

            modelBuilder.Entity<MouvementStock>()
                .Property(ms => ms.QuantiteAvant)
                .HasPrecision(18, 4);

            modelBuilder.Entity<MouvementStock>()
                .Property(ms => ms.QuantiteApres)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Article>()
                .Property(a => a.PrixUnitaireMoyen)
                .HasPrecision(18, 4);

            // Index pour améliorer les performances
            modelBuilder.Entity<Stock>()
                .HasIndex(s => new { s.ArticleId, s.TypeStock });

            // Traçabilité réception (Fonctionnalité 18) — accès au stock par ligne d'origine
            modelBuilder.Entity<Stock>()
                .HasIndex(s => s.LigneAchatId);

            modelBuilder.Entity<Stock>()
                .HasIndex(s => s.LigneImportationId);

            modelBuilder.Entity<CommandeClient>()
                .HasIndex(cc => cc.NumeroCommande)
                .IsUnique();

            modelBuilder.Entity<Achat>()
                .HasIndex(a => a.NumeroAchat)
                .IsUnique();

            modelBuilder.Entity<Importation>()
                .HasIndex(i => i.ReferenceImportation)
                .IsUnique();

            modelBuilder.Entity<MouvementStock>()
                .HasIndex(ms => ms.DateMouvement);

            modelBuilder.Entity<TacheProduction>()
                .HasIndex(tp => new { tp.Statut, tp.DateFinPrevue });

            // Configuration des enums pour PostgreSQL
            modelBuilder.Entity<Role>().ToTable("Role");
            
            modelBuilder.Entity<Stock>()
                .Property(s => s.TypeStock)
                .HasConversion<string>();

            modelBuilder.Entity<CommandeClient>()
                .Property(cc => cc.Statut)
                .HasConversion<string>();

            modelBuilder.Entity<TacheProduction>()
                .Property(tp => tp.Statut)
                .HasConversion<string>();

            modelBuilder.Entity<TacheProduction>()
                .Property(tp => tp.Priorite)
                .HasConversion<string>();

            modelBuilder.Entity<Achat>()
                .Property(a => a.Statut)
                .HasConversion<string>();

            modelBuilder.Entity<Achat>()
                .Property(a => a.TypePaiement)
                .HasConversion<string>();

            modelBuilder.Entity<Importation>()
                .Property(i => i.Statut)
                .HasConversion<string>();

            modelBuilder.Entity<Importation>()
                .Property(i => i.ModeExpedition)
                .HasConversion<string>();

            modelBuilder.Entity<MouvementStock>()
                .Property(ms => ms.TypeMouvement)
                .HasConversion<string>();

            modelBuilder.Entity<MouvementStock>()
                .Property(ms => ms.OrigineMouvement)
                .HasConversion<string>();

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.TypeBesoin)
                .HasConversion<string>();

            // ModeleBom -> FournitureBom (One-to-Many)
            modelBuilder.Entity<FournitureBom>()
                .HasOne(f => f.ModeleBom)
                .WithMany(m => m.Fournitures)
                .HasForeignKey(f => f.ModeleBomId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FournitureBom>()
                .HasOne(f => f.Article)
                .WithMany()
                .HasForeignKey(f => f.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FournitureBom>()
                .Property(f => f.QteParPiece)
                .HasPrecision(18, 4);

            // HistoriquePrixArticle — prix de référence tracé par article
            modelBuilder.Entity<HistoriquePrixArticle>()
                .HasOne(h => h.Article)
                .WithMany()
                .HasForeignKey(h => h.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Lignes référencées : SetNull pour conserver l'entrée d'historique
            // même si l'achat/l'importation d'origine est supprimé(e).
            modelBuilder.Entity<HistoriquePrixArticle>()
                .HasOne(h => h.LigneAchat)
                .WithMany()
                .HasForeignKey(h => h.LigneAchatId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<HistoriquePrixArticle>()
                .HasOne(h => h.LigneImportation)
                .WithMany()
                .HasForeignKey(h => h.LigneImportationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<HistoriquePrixArticle>()
                .Property(h => h.PrixUnitaire)
                .HasPrecision(18, 4);

            // Source (enum) stocké en string, comme les autres enums du projet
            modelBuilder.Entity<HistoriquePrixArticle>()
                .Property(h => h.Source)
                .HasConversion<string>();

            // Lecture triée par date décroissante pour un article donné
            modelBuilder.Entity<HistoriquePrixArticle>()
                .HasIndex(h => new { h.ArticleId, h.DateEffective });

            modelBuilder.Entity<HistoriquePrixArticle>()
                .HasIndex(h => h.LigneAchatId);

            modelBuilder.Entity<HistoriquePrixArticle>()
                .HasIndex(h => h.LigneImportationId);

            // LigneImportation — TypeDestination et StatutLigne stockés en string
            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.TypeDestination)
                .HasConversion<string>();

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.StatutLigne)
                .HasConversion<string>();

            // LigneAchat — TypeDestination et StatutLigne stockés en string
            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.TypeDestination)
                .HasConversion<string>();

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.StatutLigne)
                .HasConversion<string>();

            // DocumentJoint — enum stocké en string
            modelBuilder.Entity<DocumentJoint>()
                .Property(d => d.Type)
                .HasConversion<string>();

            // Facture — enum stocké en string
            modelBuilder.Entity<Facture>()
                .Property(f => f.Statut)
                .HasConversion<string>();

            // DocumentJoint -> Achat (nullable, cascade sur suppression achat)
            modelBuilder.Entity<DocumentJoint>()
                .HasOne(d => d.Achat)
                .WithMany()
                .HasForeignKey(d => d.AchatId)
                .OnDelete(DeleteBehavior.Cascade);

            // DocumentJoint -> Importation (nullable, cascade sur suppression importation)
            modelBuilder.Entity<DocumentJoint>()
                .HasOne(d => d.Importation)
                .WithMany()
                .HasForeignKey(d => d.ImportationId)
                .OnDelete(DeleteBehavior.Cascade);

            // DocumentJoint -> CommandeClient (nullable, cascade sur suppression commande)
            modelBuilder.Entity<DocumentJoint>()
                .HasOne(d => d.CommandeClient)
                .WithMany()
                .HasForeignKey(d => d.CommandeClientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DocumentJoint>()
                .HasIndex(d => d.CommandeClientId);

            // GroupeCommande -> GroupeCommandeCommande (One-to-Many)
            modelBuilder.Entity<GroupeCommandeCommande>()
                .HasOne(gcc => gcc.GroupeCommande)
                .WithMany(gc => gc.Membres)
                .HasForeignKey(gcc => gcc.GroupeCommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            // CommandeClient -> GroupeCommandeCommande (One-to-Many)
            modelBuilder.Entity<GroupeCommandeCommande>()
                .HasOne(gcc => gcc.CommandeClient)
                .WithMany()
                .HasForeignKey(gcc => gcc.CommandeClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index unique sur (GroupeCommandeId, CommandeClientId)
            modelBuilder.Entity<GroupeCommandeCommande>()
                .HasIndex(gcc => new { gcc.GroupeCommandeId, gcc.CommandeClientId })
                .IsUnique();

            // Stock -> GroupeCommande (scope optionnel)
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.GroupeCommande)
                .WithMany()
                .HasForeignKey(s => s.GroupeCommandeId)
                .OnDelete(DeleteBehavior.Restrict);

            // LigneAchat -> GroupeCommande (scope optionnel)
            modelBuilder.Entity<LigneAchat>()
                .HasOne(la => la.GroupeCommande)
                .WithMany()
                .HasForeignKey(la => la.GroupeCommandeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // LigneImportation -> GroupeCommande (scope optionnel)
            modelBuilder.Entity<LigneImportation>()
                .HasOne(li => li.GroupeCommande)
                .WithMany()
                .HasForeignKey(li => li.GroupeCommandeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes sur les nouvelles colonnes GroupeCommandeId
            modelBuilder.Entity<Stock>()
                .HasIndex(s => s.GroupeCommandeId);

            modelBuilder.Entity<LigneAchat>()
                .HasIndex(la => la.GroupeCommandeId);

            modelBuilder.Entity<LigneImportation>()
                .HasIndex(li => li.GroupeCommandeId);

            // TauxChange -> Devise (One-to-Many, Restrict) — void supprimer une devise
            // référencée par un taux ; un taux orphelin serait source d'erreur de conversion.
            modelBuilder.Entity<TauxChange>()
                .HasOne(t => t.Devise)
                .WithMany()
                .HasForeignKey(t => t.DeviseCode)
                .OnDelete(DeleteBehavior.Restrict);

            // Index (DeviseCode, DateEffective) pour retrouver rapidement le taux applicable
            // le plus récent pour une devise donnée.
            modelBuilder.Entity<TauxChange>()
                .HasIndex(t => new { t.DeviseCode, t.DateEffective });

            modelBuilder.Entity<TauxChange>()
                .Property(t => t.Taux)
                .HasPrecision(18, 6);

            // ═══════════════════════════════════════════════════════════════════
            // Module « Fournitures liées aux pièces coupées » + Sous-traitance
            // multi-chaînes (partie corrective).
            // ═══════════════════════════════════════════════════════════════════

            // Article parent/enfant (variante légère : self-FK + taille optionnelle)
            modelBuilder.Entity<Article>()
                .HasOne(a => a.ArticleParent)
                .WithMany(a => a.Variantes)
                .HasForeignKey(a => a.ArticleParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index dédié (nommage explicite ArticleParentId + Taille)
            modelBuilder.Entity<Article>()
                .HasIndex(a => new { a.ArticleParentId, a.Taille })
                .HasDatabaseName("IX_Articles_ParentEnfant");

            // Matelas — numéro unique
            modelBuilder.Entity<Matelas>()
                .HasIndex(m => m.NumeroMatelas)
                .IsUnique();

            // LotCoupe -> Matelas (nullable, SetNull pour conserver l'historique)
            modelBuilder.Entity<LotCoupe>()
                .HasOne(lc => lc.Matelas)
                .WithMany(m => m.LotCoupes)
                .HasForeignKey(lc => lc.MatelasId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LotCoupe>()
                .HasIndex(lc => new { lc.CommandeId, lc.MatelasId });

            // ChaineProduction — nom unique, type stocké en string
            modelBuilder.Entity<ChaineProduction>()
                .HasIndex(cp => cp.Nom)
                .IsUnique();

            modelBuilder.Entity<ChaineProduction>()
                .Property(cp => cp.TypeChaine)
                .HasConversion<string>();

            // LotExport -> ChaineProduction (nullable, SetNull ; filtre/affichage uniquement)
            modelBuilder.Entity<LotExport>()
                .HasOne(le => le.ChaineProduction)
                .WithMany(cp => cp.LotExports)
                .HasForeignKey(le => le.ChaineProductionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LotExport>()
                .HasIndex(le => new { le.CommandeId, le.ChaineProductionId });

            // FournitureCommandeLigne (Commande -> Lignes ; Article -> Lignes)
            modelBuilder.Entity<FournitureCommandeLigne>()
                .HasOne(f => f.Commande)
                .WithMany(c => c.FournituresLignes)
                .HasForeignKey(f => f.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FournitureCommandeLigne>()
                .HasOne(f => f.Article)
                .WithMany()
                .HasForeignKey(f => f.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FournitureCommandeLigne>()
                .Property(f => f.QuantiteFourniture)
                .HasPrecision(18, 4);

            // Enum stocké en string (règle projet, Bug 13 précédent)
            modelBuilder.Entity<FournitureCommandeLigne>()
                .Property(f => f.Portee)
                .HasConversion<string>();

            // Lecture rapide des lignes d'une commande (même article + taille)
            modelBuilder.Entity<FournitureCommandeLigne>()
                .HasIndex(f => new { f.CommandeId, f.ArticleId, f.Taille });

            // ReceptionFourniture (cumulatif, sans plafond)
            modelBuilder.Entity<ReceptionFourniture>()
                .HasOne(r => r.CommandeLigne)
                .WithMany(l => l.Receptions)
                .HasForeignKey(r => r.CommandeFournitureLigneId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReceptionFourniture>()
                .Property(r => r.QuantiteRecue)
                .HasPrecision(18, 4);

            modelBuilder.Entity<ReceptionFourniture>()
                .HasIndex(r => r.CommandeFournitureLigneId);

            // EnvoiFourniture (plafond global multi-chaînes + ForcerDepassement)
            modelBuilder.Entity<EnvoiFourniture>()
                .HasOne(e => e.CommandeLigne)
                .WithMany(l => l.Envois)
                .HasForeignKey(e => e.CommandeFournitureLigneId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EnvoiFourniture>()
                .HasOne(e => e.ChaineProduction)
                .WithMany(cp => cp.EnvoisFourniture)
                .HasForeignKey(e => e.ChaineProductionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<EnvoiFourniture>()
                .Property(e => e.QuantiteEnvoyee)
                .HasPrecision(18, 4);

            modelBuilder.Entity<EnvoiFourniture>()
                .HasIndex(e => new { e.CommandeFournitureLigneId, e.ChaineProductionId });

            // ═══════════════════════════════════════════════════════════════════
            // Parc machines à coudre — module indépendant (inventaire + maintenance).
            // ═══════════════════════════════════════════════════════════════════

            // CodeMachine unique
            modelBuilder.Entity<Machine>()
                .HasIndex(m => m.CodeMachine)
                .IsUnique();

            // Enums stockés en string (règle projet)
            modelBuilder.Entity<Machine>()
                .Property(m => m.TypeMachine)
                .HasConversion<string>();

            modelBuilder.Entity<Machine>()
                .Property(m => m.Statut)
                .HasConversion<string>();

            // InterventionMachine -> Machine (Cascade : la journalisation des
            // interventions vit avec la machine)
            modelBuilder.Entity<InterventionMachine>()
                .HasOne(i => i.Machine)
                .WithMany(m => m.Interventions)
                .HasForeignKey(i => i.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InterventionMachine>()
                .HasIndex(i => i.MachineId);

            modelBuilder.Entity<InterventionMachine>()
                .Property(i => i.TypeIntervention)
                .HasConversion<string>();

            modelBuilder.Entity<InterventionMachine>()
                .Property(i => i.CoutIntervention)
                .HasPrecision(18, 4);

            // ═══════════════════════════════════════════════════════════════════
            // Ordres de fabrication — Phase 1 (structure + étiquetage).
            // Couche additif scoping CommandeClient : aucun code de calcul
            // (Calculer/ValiderRessources) ne référence ces entités.
            // ═══════════════════════════════════════════════════════════════════

            // OrdreFabrication -> Commande (Cascade) ; ChaineProduction (SetNull)
            modelBuilder.Entity<OrdreFabrication>()
                .HasOne(of => of.Commande)
                .WithMany(c => c.OrdresFabrication)
                .HasForeignKey(of => of.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrdreFabrication>()
                .HasOne(of => of.ChaineProduction)
                .WithMany()
                .HasForeignKey(of => of.ChaineProductionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Numéro d'OF unique au sein de la commande (Décision 5)
            modelBuilder.Entity<OrdreFabrication>()
                .HasIndex(of => new { of.CommandeId, of.NumeroOF })
                .IsUnique();

            // OrdreFabricationTailleLigne -> OrdreFabrication (Cascade)
            modelBuilder.Entity<OrdreFabricationTailleLigne>()
                .HasOne(t => t.OrdreFabrication)
                .WithMany(of => of.Tailles)
                .HasForeignKey(t => t.OrdreFabricationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrdreFabricationTailleLigne>()
                .HasIndex(t => t.OrdreFabricationId);

            // OrdreFabricationEtiquette -> OrdreFabrication (Cascade) ;
            // FournitureCommandeLigne (SetNull : l'étiquette survit à la ligne)
            modelBuilder.Entity<OrdreFabricationEtiquette>()
                .HasOne(e => e.OrdreFabrication)
                .WithMany(of => of.Etiquettes)
                .HasForeignKey(e => e.OrdreFabricationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrdreFabricationEtiquette>()
                .HasOne(e => e.FournitureLigne)
                .WithMany()
                .HasForeignKey(e => e.FournitureCommandeLigneId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrdreFabricationEtiquette>()
                .HasIndex(e => e.OrdreFabricationId);

            // LotCoupe -> OrdreFabrication (nullable, SetNull : l'historique des
            // coupes n'est jamais supprimé avec l'OF)
            modelBuilder.Entity<LotCoupe>()
                .HasOne(lc => lc.OrdreFabrication)
                .WithMany()
                .HasForeignKey(lc => lc.OrdreFabricationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LotCoupe>()
                .HasIndex(lc => new { lc.CommandeId, lc.OrdreFabricationId });
        }
    }
}

