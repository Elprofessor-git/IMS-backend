using Microsoft.AspNetCore.Identity;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Create default roles
            await CreateRoles(roleManager);

            // Create default admin
            await CreateDefaultAdmin(userManager);

            // Create test data if database is empty
            if (!context.Plateformes.Any())
            {
                await CreateTestData(context);
            }
        }

        private static async Task CreateRoles(RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames = { "Admin", "Magasinier", "Acheteur", "ProductionManager", "Viewer" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        private static async Task CreateDefaultAdmin(UserManager<ApplicationUser> userManager)
        {
            var adminEmail = "admin@gestiontextile.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    Nom = "Administrateur",
                    Prenom = "Système",
                    Poste = "Administrateur Système",
                    EstActif = true,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }

        private static async Task CreateTestData(ApplicationDbContext context)
        {
            // Créer des plateformes
            var plateformes = new List<Plateforme>
            {
                new Plateforme { Nom = "Amazon", Description = "Marketplace Amazon", SiteWeb = "https://amazon.fr", ContactEmail = "contact@amazon.fr" },
                new Plateforme { Nom = "Zalando", Description = "Plateforme mode Zalando", SiteWeb = "https://zalando.fr", ContactEmail = "contact@zalando.fr" },
                new Plateforme { Nom = "Vente Directe", Description = "Ventes directes en magasin", SiteWeb = "", ContactEmail = "vente@entreprise.com" }
            };

            context.Plateformes.AddRange(plateformes);
            await context.SaveChangesAsync();

            // Créer des clients
            var clients = new List<Client>
            {
                new Client 
                { 
                    Nom = "Dupont", 
                    Prenom = "Marie", 
                    Email = "marie.dupont@email.com", 
                    PlateformeId = plateformes[0].Id,
                    Ville = "Paris",
                    Pays = "France",
                    PreferencesTissus = "Coton bio, Lin naturel"
                },
                new Client 
                { 
                    Nom = "Martin", 
                    Prenom = "Jean", 
                    Email = "jean.martin@email.com", 
                    PlateformeId = plateformes[1].Id,
                    Ville = "Lyon",
                    Pays = "France",
                    PreferencesTissus = "Soie, Laine mérinos"
                },
                new Client 
                { 
                    NomEntreprise = "Fashion4U", 
                    Email = "commandes@fashion4u.com", 
                    PlateformeId = plateformes[0].Id,
                    Ville = "Marseille",
                    Pays = "France",
                    PreferencesTissus = "Tissus techniques, Polyester recyclé"
                }
            };

            context.Clients.AddRange(clients);
            await context.SaveChangesAsync();

            // Créer des fournisseurs
            var fournisseurs = new List<Fournisseur>
            {
                new Fournisseur 
                { 
                    NomEntreprise = "Textiles Milano S.p.A", 
                    PersonneContact = "Giuseppe Rossi", 
                    Email = "giuseppe@textilesmilano.it",
                    Ville = "Milano",
                    Pays = "Italie",
                    SpecialitesProduits = "Tissus haute couture, Soie italienne",
                    DelaiLivraisonJours = 14
                },
                new Fournisseur 
                { 
                    NomEntreprise = "Cotton World Ltd", 
                    PersonneContact = "Sarah Johnson", 
                    Email = "sarah@cottonworld.com",
                    Ville = "Manchester",
                    Pays = "Royaume-Uni",
                    SpecialitesProduits = "Coton bio, Tissus durables",
                    DelaiLivraisonJours = 10
                },
                new Fournisseur 
                { 
                    NomEntreprise = "Accessoires Pro", 
                    PersonneContact = "Pierre Dubois", 
                    Email = "pierre@accessoirespro.fr",
                    Ville = "Roubaix",
                    Pays = "France",
                    SpecialitesProduits = "Boutons, Fermetures, Étiquettes",
                    DelaiLivraisonJours = 5
                }
            };

            context.Fournisseurs.AddRange(fournisseurs);
            await context.SaveChangesAsync();

            // Créer des articles
            var articles = new List<Article>
            {
                new Article 
                { 
                    Designation = "Tissu Coton Bio", 
                    Description = "Tissu 100% coton biologique certifié GOTS",
                    Categorie = "Tissu",
                    SousCategorie = "Coton",
                    Unite = "mètre",
                    Reference = "COT-BIO-001",
                    SeuilAlerte = 50,
                    SeuilCritique = 20,
                    PrixUnitaireMoyen = 12.50m
                },
                new Article 
                { 
                    Designation = "Boutons Nacre", 
                    Description = "Boutons en nacre naturelle 15mm",
                    Categorie = "Accessoire",
                    SousCategorie = "Bouton",
                    Unite = "pièce",
                    Reference = "BTN-NAC-15",
                    SeuilAlerte = 1000,
                    SeuilCritique = 500,
                    PrixUnitaireMoyen = 0.25m
                },
                new Article 
                { 
                    Designation = "Fil Polyester", 
                    Description = "Fil à coudre polyester haute résistance",
                    Categorie = "Fil",
                    SousCategorie = "Polyester",
                    Unite = "bobine",
                    Reference = "FIL-POL-001",
                    SeuilAlerte = 100,
                    SeuilCritique = 50,
                    PrixUnitaireMoyen = 3.20m
                },
                new Article 
                { 
                    Designation = "Étiquettes Composition", 
                    Description = "Étiquettes de composition textile",
                    Categorie = "Étiquette",
                    SousCategorie = "Composition",
                    Unite = "pièce",
                    Reference = "ETQ-COMP-001",
                    SeuilAlerte = 2000,
                    SeuilCritique = 1000,
                    PrixUnitaireMoyen = 0.05m
                }
            };

            context.Articles.AddRange(articles);
            await context.SaveChangesAsync();

            // Créer du stock initial
            var stocks = new List<Stock>
            {
                new Stock 
                { 
                    ArticleId = articles[0].Id, 
                    Couleur = "Blanc", 
                    CodeCouleur = "#FFFFFF",
                    Quantite = 150,
                    TypeStock = TypeStock.Libre,
                    PrixUnitaire = 12.50m,
                    EmplacementPhysique = "A1-01"
                },
                new Stock 
                { 
                    ArticleId = articles[0].Id, 
                    Couleur = "Bleu Marine", 
                    CodeCouleur = "#000080",
                    Quantite = 80,
                    TypeStock = TypeStock.Libre,
                    PrixUnitaire = 12.50m,
                    EmplacementPhysique = "A1-02"
                },
                new Stock 
                { 
                    ArticleId = articles[1].Id, 
                    Couleur = "Blanc", 
                    CodeCouleur = "#FFFFFF",
                    Quantite = 2500,
                    TypeStock = TypeStock.Libre,
                    PrixUnitaire = 0.25m,
                    EmplacementPhysique = "B2-01"
                },
                new Stock 
                { 
                    ArticleId = articles[2].Id, 
                    Couleur = "Blanc", 
                    CodeCouleur = "#FFFFFF",
                    Quantite = 150,
                    TypeStock = TypeStock.Libre,
                    PrixUnitaire = 3.20m,
                    EmplacementPhysique = "C1-01"
                },
                new Stock 
                { 
                    ArticleId = articles[3].Id, 
                    Couleur = "Blanc", 
                    CodeCouleur = "#FFFFFF",
                    Quantite = 5000,
                    TypeStock = TypeStock.Libre,
                    PrixUnitaire = 0.05m,
                    EmplacementPhysique = "D1-01"
                }
            };

            context.Stocks.AddRange(stocks);
            await context.SaveChangesAsync();

            // Créer une commande client exemple
            var commande = new CommandeClient
            {
                NumeroCommande = "CMD202501001",
                ClientId = clients[0].Id,
                TitreCommande = "Commande Chemises Coton Bio",
                DescriptionCommande = "Production de 100 chemises en coton bio blanc",
                DateLivraisonSouhaitee = DateTime.Now.AddDays(30),
                MontantTotal = 2500.00m,
                Statut = StatutCommande.EnAttente
            };

            context.CommandesClients.Add(commande);
            await context.SaveChangesAsync();

            // Créer les besoins pour cette commande
            var besoins = new List<BesoinCommande>
            {
                new BesoinCommande
                {
                    CommandeClientId = commande.Id,
                    ArticleId = articles[0].Id, // Tissu coton bio
                    TypeBesoin = TypeBesoin.MatierePremiere,
                    Couleur = "Blanc",
                    QuantiteUnitaire = 1.5m, // 1.5m par chemise
                    NombrePieces = 100,
                    QuantiteTotale = 150m
                },
                new BesoinCommande
                {
                    CommandeClientId = commande.Id,
                    ArticleId = articles[1].Id, // Boutons
                    TypeBesoin = TypeBesoin.Accessoire,
                    Couleur = "Blanc",
                    QuantiteUnitaire = 8, // 8 boutons par chemise
                    NombrePieces = 100,
                    QuantiteTotale = 800
                },
                new BesoinCommande
                {
                    CommandeClientId = commande.Id,
                    ArticleId = articles[2].Id, // Fil
                    TypeBesoin = TypeBesoin.Accessoire,
                    Couleur = "Blanc",
                    QuantiteUnitaire = 0.5m, // 0.5 bobine par chemise
                    NombrePieces = 100,
                    QuantiteTotale = 50
                },
                new BesoinCommande
                {
                    CommandeClientId = commande.Id,
                    ArticleId = articles[3].Id, // Étiquettes
                    TypeBesoin = TypeBesoin.Emballage,
                    Couleur = "Blanc",
                    QuantiteUnitaire = 2, // 2 étiquettes par chemise
                    NombrePieces = 100,
                    QuantiteTotale = 200
                }
            };

            context.BesoinsCommandes.AddRange(besoins);
            await context.SaveChangesAsync();

            Console.WriteLine("Données de test créées avec succès !");
        }
    }
}

