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
        public DbSet<DocumentImportation> DocumentsImportation { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuration des relations et contraintes

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

            // CommandeClient -> Achat (One-to-Many)
            modelBuilder.Entity<Achat>()
                .HasOne(a => a.CommandeClient)
                .WithMany(cc => cc.Achats)
                .HasForeignKey(a => a.CommandeClientId)
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

            // Fournisseur -> Importation (One-to-Many)
            modelBuilder.Entity<Importation>()
                .HasOne(i => i.Fournisseur)
                .WithMany(f => f.Importations)
                .HasForeignKey(i => i.FournisseurId)
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

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteUnitaire)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteTotale)
                .HasPrecision(18, 4);

            modelBuilder.Entity<BesoinCommande>()
                .Property(bc => bc.QuantiteCouverte)
                .HasPrecision(18, 4);

            modelBuilder.Entity<CommandeClient>()
                .Property(cc => cc.MontantTotal)
                .HasPrecision(18, 4);

            modelBuilder.Entity<CommandeClient>()
                .Property(cc => cc.PourcentageRessourcesCouvertes)
                .HasPrecision(5, 2);

            modelBuilder.Entity<TacheProduction>()
                .Property(tp => tp.PourcentageAvancement)
                .HasPrecision(5, 2);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.Quantite)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.PrixUnitaire)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneAchat>()
                .Property(la => la.MontantLigne)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Achat>()
                .Property(a => a.MontantTotal)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.Quantite)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.PrixUnitaire)
                .HasPrecision(18, 4);

            modelBuilder.Entity<LigneImportation>()
                .Property(li => li.MontantLigne)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Importation>()
                .Property(i => i.MontantTotal)
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
        }
    }
}

