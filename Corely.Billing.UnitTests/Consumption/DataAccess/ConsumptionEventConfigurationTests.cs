using Corely.Billing.Consumption.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Corely.Billing.UnitTests.Consumption.DataAccess;

public class ConsumptionEventConfigurationTests
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

        // Unique is the point. Without it a retry writes a second row and bills the work twice.
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

        // Was unique, and could not stay that way: one unit of work spanning two grants writes one
        // row per grant under a single correlation id.
        Assert.NotNull(index);
        Assert.False(index!.IsUnique);
    }

    private static IEntityType BuildEntityType()
    {
        var ctx = new MeteringDbContext(new DummyConfig());
        var entity = ctx.Model.FindEntityType(typeof(ConsumptionEventEntity));
        Assert.NotNull(entity);
        return entity!;
    }

    private static IIndex? FindIndex(IEntityType entity, params string[] properties) =>
        entity
            .GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(properties));

    // Only the model is inspected here, but the provider still shapes it, so use the same
    // relational provider the rest of the unit tier runs on rather than the InMemory one.
    private sealed class DummyConfig() : EFSqliteConfigurationBase("Data Source=:memory:")
    {
        public override void Configure(DbContextOptionsBuilder b) => b.UseSqlite(connectionString);
    }
}
