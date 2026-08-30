using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wanes.DataAccess;

/// <summary>
/// Used only by EF Core tooling (migrations) so it never has to run the web host.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseService>
{
    public DatabaseService CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DatabaseService>()
            .UseSqlServer(
                "Server=localhost;Database=Wanes;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.UseNetTopologySuite())
            .Options;

        return new DatabaseService(options);
    }
}
