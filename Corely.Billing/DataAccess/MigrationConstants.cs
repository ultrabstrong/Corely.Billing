namespace Corely.Billing.DataAccess;

internal static class MigrationConstants
{
    /// <summary>
    /// Kept separate from the default __EFMigrationsHistory so billing can share a database with a
    /// consumer's own contexts, and with Corely.IAM's.
    /// </summary>
    public const string DEFAULT_HISTORY_TABLE = "__CorelyBillingMigrationsHistory";
}
