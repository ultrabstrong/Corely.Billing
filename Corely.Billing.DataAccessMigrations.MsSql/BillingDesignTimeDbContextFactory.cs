using Corely.Billing.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Corely.Billing.DataAccessMigrations.MsSql;

internal class BillingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    private const string DESIGN_TIME_MARKER = "designtimeonly";
    private const string DESIGN_TIME_CONNECTION_STRING =
        $"Server={DESIGN_TIME_MARKER};Database={DESIGN_TIME_MARKER};Trusted_Connection=True;TrustServerCertificate=True;";

    public BillingDbContext CreateDbContext(string[] args)
    {
        var configuration = new EFMsSqlConfiguration(DESIGN_TIME_CONNECTION_STRING);
        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();
        configuration.Configure(optionsBuilder);
        return new BillingDbContext(configuration);
    }
}
