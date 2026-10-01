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

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Rôle administrateur (Id=1) AVANT tout utilisateur : ApplicationUser.RoleId
            // est une FK obligatoire — sur base vide, la création de l'admin plantait
            // (clé étrangère RoleId=1 absente de la table Role).
            await EnsureAdminRole(context);

            // Create default admin
            await CreateDefaultAdmin(userManager);
        }

        private static async Task EnsureAdminRole(ApplicationDbContext context)
        {
            var adminRole = await context.AppRoles.FindAsync(1);
            if (adminRole != null)
                return;

            context.AppRoles.Add(new Role
            {
                Id = 1,
                NomRole = "Administrateur",
                Description = "Rôle administrateur système",
                EstAdministrateur = true,
                EstActif = true,
                PeutGererStock = true,
                PeutGererCommandes = true,
                PeutGererTaches = true,
                // Droits de RESSOURCE sur les tâches (LOT 16) : l'administrateur les
                // possède de fait (EstAdministrateur), on les pose explicitement pour
                // que l'UI d'édition des rôles reste cohérente.
                PeutVoirToutesTaches = true,
                PeutAssignerTaches = true,
                PeutGererClients = true,
                PeutGererFournisseurs = true,
                PeutGererAchats = true,
                PeutGererImportations = true,
                PeutGererUtilisateurs = true,
                PeutGererMouvements = true,
                PeutGererPlateformes = true,
                PeutVoirMouvements = true,
                PeutVoirCommandes = true,
                PeutVoirClients = true,
                PeutVoirFournisseurs = true,
                PeutVoirPlateformes = true,
                PeutVoirTaches = true,
                PeutVoirUtilisateurs = true,
                PeutVoirRoles = true,
                PeutValiderStock = true,
                PeutConfirmerAchats = true,
                PeutValiderImportations = true,
                // L'administrateur peut créer des liens de partage. Sans cette ligne,
                // le rôle Id=1 resterait à false et AUCUN utilisateur ne pourrait
                // jamais créer un lien : EnsureAdminRole sort tôt (ligne 28-30) sur
                // une base existante, donc la valeur devrait être corrigée à la main
                // en SQL. On la pose explicitement à la création.
                PeutPartagerLiens = true,
                PeutVoirDashboard = true,
                PeutVoirRapports = true,
                PeutVoirFactures = true,
                PeutGererFactures = true,
                PeutVoirMachines = true,
                PeutGererMachines = true,
                PeutVoirCoupe = true,
                PeutGererCoupe = true,
                PeutVoirPlanning = true,
                PeutGererPlanning = true,
                PeutVoirProduction = true,
                PeutGererProduction = true,
                PeutVoirQualite = true,
                PeutGererQualite = true,
                PeutVoirCourriels = true,
                PeutGererCourriels = true,
                DateCreation = DateTime.Now
            });
            await context.SaveChangesAsync();
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
                    EmailConfirmed = true,
                    // RoleId 1 = Administrateur (table Role)
                    RoleId = 1
                };

                await userManager.CreateAsync(adminUser, "Admin123!");
            }
        }
    }
}
