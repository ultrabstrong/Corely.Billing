using Corely.Billing.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Corely.Billing.DataAccessMigrations.MySql;

internal class BillingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var configuration = new EFMySqlConfiguration(
            MySqlDesignTimeConstants.DesignTimeConnectionString
        );
        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();
        configuration.Configure(optionsBuilder);
        return new BillingDbContext(configuration);
    }
}
