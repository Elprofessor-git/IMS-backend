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
    }
}

