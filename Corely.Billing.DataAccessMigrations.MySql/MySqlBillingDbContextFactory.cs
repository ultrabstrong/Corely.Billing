using Corely.Billing.DataAccess;

namespace Corely.Billing.DataAccessMigrations.MySql;

internal static class MySqlBillingDbContextFactory
{
    public static BillingDbContext Create(string connectionString, string? historyTable = null) =>
        new(new EFMySqlConfiguration(connectionString, historyTable));
}
