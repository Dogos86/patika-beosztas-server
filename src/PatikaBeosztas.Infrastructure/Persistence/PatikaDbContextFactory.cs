using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PatikaBeosztas.Infrastructure.Persistence;

public sealed class PatikaDbContextFactory : IDesignTimeDbContextFactory<PatikaDbContext>
{
    public PatikaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A ConnectionStrings__DefaultConnection környezeti változó szükséges a migrációs parancshoz.");
        }

        var options = new DbContextOptionsBuilder<PatikaDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PatikaDbContext(options);
    }
}
