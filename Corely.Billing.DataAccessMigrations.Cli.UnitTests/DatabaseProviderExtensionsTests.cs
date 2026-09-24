namespace Corely.Billing.DataAccessMigrations.Cli.UnitTests;

public class DatabaseProviderExtensionsTests
{
    [Theory]
    [InlineData(DatabaseProvider.MsSql, "Trusted_Connection=True")]
    [InlineData(DatabaseProvider.MySql, "Uid=root")]
    public void PlaceholderConnectionString_UsesTheProvidersSyntax_ForEachProvider(
        DatabaseProvider provider,
        string expected
    )
    {
        var connectionString = provider.PlaceholderConnectionString();

        Assert.Contains(expected, connectionString);
        Assert.Contains("Database=CorelyBilling", connectionString);
    }
}
