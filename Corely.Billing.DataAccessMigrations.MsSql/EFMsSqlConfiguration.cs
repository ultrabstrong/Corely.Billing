using Corely.Billing.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Corely.Billing.DataAccessMigrations.MsSql;

internal class EFMsSqlConfiguration(string connectionString, string? historyTable = null)
    : EFMsSqlConfigurationBase(connectionString)
{
    public override void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            connectionString,
            b =>
            {
                b.MigrationsAssembly(typeof(EFMsSqlConfiguration).Assembly.GetName().Name);
                b.MigrationsHistoryTable(
                    historyTable ?? BillingDbContext.DEFAULT_MIGRATIONS_HISTORY_TABLE
                );
            }
        );
    }
}
