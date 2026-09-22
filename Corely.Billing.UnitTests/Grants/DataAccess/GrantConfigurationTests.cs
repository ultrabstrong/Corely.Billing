using Corely.Billing.Grants.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Corely.Billing.UnitTests.Grants.DataAccess;

public class GrantConfigurationTests
{
    [Fact]
    public void Model_Builds_And_Configures_Indexes()
    {
        // Build a context that uses the in-memory provider
        var ctx = new EntitlementsDbContext(new DummyConfig());
        var model = ctx.Model; // force OnModelCreating

        // Ensure entity is present
        var entity = model.FindEntityType(typeof(GrantEntity));
        Assert.NotNull(entity);

        // Grants are only ever read account-scoped, so the AccountId index has to be there. It
        // must not be unique: GrantId is the primary key, so uniqueness is already global and a
        // unique index here would enforce nothing.
        var accountIndex = Assert.Single(
            entity!.GetIndexes(),
            i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(GrantEntity.AccountId)])
        );
        Assert.False(accountIndex.IsUnique);
    }

    // Only the model is inspected here, but the provider still shapes it, so use the same
    // relational provider the rest of the unit tier runs on rather than the InMemory one.
    private sealed class DummyConfig() : EFSqliteConfigurationBase("Data Source=:memory:")
    {
        public override void Configure(DbContextOptionsBuilder b) => b.UseSqlite(connectionString);
    }
}
