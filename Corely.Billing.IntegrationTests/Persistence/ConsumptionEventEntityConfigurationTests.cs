using Corely.Billing.Consumption.Entities;
using Corely.Billing.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Corely.Billing.IntegrationTests.Persistence;

public class ConsumptionEventEntityConfigurationTests
{
    [Fact]
    public void Model_ConfiguresAUniqueIdempotencyIndex_ForConsumptionEvents()
    {
        var entity = BuildEntityType();

        var index = FindIndex(
            entity,
            nameof(ConsumptionEventEntity.AccountId),
            nameof(ConsumptionEventEntity.IdempotencyKey)
        );

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void Model_ConfiguresANonUniqueCorrelationIndex_ForConsumptionEvents()
    {
        var entity = BuildEntityType();

        var index = FindIndex(
            entity,
            nameof(ConsumptionEventEntity.AccountId),
            nameof(ConsumptionEventEntity.CorrelationId)
        );

        Assert.NotNull(index);
        Assert.False(index!.IsUnique);
    }

    private static IEntityType BuildEntityType()
    {
        var ctx = new BillingDbContext(new DummyConfig());
        var entity = ctx.Model.FindEntityType(typeof(ConsumptionEventEntity));
        Assert.NotNull(entity);
        return entity!;
    }

    private static IIndex? FindIndex(IEntityType entity, params string[] properties) =>
        entity
            .GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(properties));

    private sealed class DummyConfig() : EFSqliteConfigurationBase("Data Source=:memory:")
    {
        public override void Configure(DbContextOptionsBuilder b) => b.UseSqlite(connectionString);
    }
}
