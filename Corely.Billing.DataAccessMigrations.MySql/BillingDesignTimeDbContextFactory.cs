using Corely.Billing.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Corely.Billing.DataAccessMigrations.MySql;

internal class BillingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    private const string DESIGN_TIME_MARKER = "designtimeonly";
    private const string DESIGN_TIME_CONNECTION_STRING =
        $"Server={DESIGN_TIME_MARKER};Port=1;Database={DESIGN_TIME_MARKER};Uid={DESIGN_TIME_MARKER};Pwd={DESIGN_TIME_MARKER};";

    public BillingDbContext CreateDbContext(string[] args)
    {
        var configuration = new EFMySqlConfiguration(DESIGN_TIME_CONNECTION_STRING);
        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();
        configuration.Configure(optionsBuilder);
        return new BillingDbContext(configuration);
    }
}
