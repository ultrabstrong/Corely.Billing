using Corely.Billing.DataAccess;
using Corely.Billing.Grants.Entities;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Corely.Billing.IntegrationTests.Persistence;

public class GrantEntityConfigurationTests
{
    [Fact]
    public void Model_Builds_And_Configures_Indexes()
    {
        var ctx = new BillingDbContext(new DummyConfig());
        var model = ctx.Model;

        var entity = model.FindEntityType(typeof(GrantEntity));
        Assert.NotNull(entity);

        var accountIndex = Assert.Single(
            entity!.GetIndexes(),
            i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(GrantEntity.AccountId)])
        );
        Assert.False(accountIndex.IsUnique);
    }

    private sealed class DummyConfig() : EFSqliteConfigurationBase("Data Source=:memory:")
    {
        public override void Configure(DbContextOptionsBuilder b) => b.UseSqlite(connectionString);
    }
}
