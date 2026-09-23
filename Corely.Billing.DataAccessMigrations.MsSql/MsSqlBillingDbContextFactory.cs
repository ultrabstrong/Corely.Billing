using Corely.Billing.DataAccess;

namespace Corely.Billing.DataAccessMigrations.MsSql;

internal static class MsSqlBillingDbContextFactory
{
    public static BillingDbContext Create(string connectionString, string? historyTable = null) =>
        new(new EFMsSqlConfiguration(connectionString, historyTable));
}
