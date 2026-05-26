using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend_Gestion_Magasin_API.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
                ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=Frigoriste3.;Pooling=true;CommandTimeout=300";

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
