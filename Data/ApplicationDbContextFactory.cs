using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend_Gestion_Magasin_API.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Aucun repli codé en dur : la chaîne vient exclusivement de l'environnement.
            // Absente, on échoue explicitement plutôt que de s'appuyer sur un secret littéral.
            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
                ?? throw new InvalidOperationException(
                    "La variable d'environnement DB_CONNECTION est absente. Posez-la avec la chaîne " +
                    "de connexion PostgreSQL (voir backend/.env.example) avant d'utiliser cette fabrique.");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
