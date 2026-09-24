namespace Corely.Billing.DataAccessMigrations.Cli;

public enum DatabaseProvider
{
    MySql,
    MsSql,
}

public static class DatabaseProviderExtensions
{
    public static bool TryParse(string? value, out DatabaseProvider provider)
    {
        if (Enum.TryParse(value, ignoreCase: true, out provider))
        {
            return true;
        }

        provider = default;
        return false;
    }

    public static string[] GetNames() => Enum.GetNames<DatabaseProvider>();

    extension(DatabaseProvider provider)
    {
        public string PlaceholderConnectionString() =>
            provider switch
            {
                DatabaseProvider.MsSql =>
                    "Server=.;Database=CorelyBilling;Trusted_Connection=True;",
                _ => "Server=localhost;Database=CorelyBilling;Uid=root;Pwd=;",
            };
    }
}
