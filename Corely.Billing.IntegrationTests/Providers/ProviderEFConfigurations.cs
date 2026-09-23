using Corely.Billing.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Corely.Billing.IntegrationTests.Providers;

internal sealed class TestMsSqlConfiguration(string connectionString)
    : EFMsSqlConfigurationBase(connectionString)
{
    public const string MIGRATIONS_ASSEMBLY = "Corely.Billing.DataAccessMigrations.MsSql";

    public override void Configure(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlServer(
            connectionString,
            b =>
            {
                b.MigrationsAssembly(MIGRATIONS_ASSEMBLY);
                b.MigrationsHistoryTable(MigrationConstants.DEFAULT_HISTORY_TABLE);
            }
        );
}

internal sealed class TestMySqlConfiguration(string connectionString)
    : EFMySqlConfigurationBase(connectionString)
{
    public const string MIGRATIONS_ASSEMBLY = "Corely.Billing.DataAccessMigrations.MySql";

    public override void Configure(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseMySQL(
            connectionString,
            b =>
            {
                b.MigrationsAssembly(MIGRATIONS_ASSEMBLY);
                b.MigrationsHistoryTable(MigrationConstants.DEFAULT_HISTORY_TABLE);
            }
        );
}
