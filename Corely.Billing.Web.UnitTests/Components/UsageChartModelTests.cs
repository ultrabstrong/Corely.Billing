using Corely.Billing.Consumption.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageChartModelTests : BillingWebTestContext
{
    private static readonly DateTime Start = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(7, TimeBucket.Day)]
    [InlineData(31, TimeBucket.Day)]
    [InlineData(90, TimeBucket.Week)]
    [InlineData(365, TimeBucket.Month)]
    public void BucketFor_PicksTheBucket_ForTheRangeLength(int days, TimeBucket expected) =>
        Assert.Equal(expected, UsageChartModel.BucketFor(Start, Start.AddDays(days)));

    [Fact]
    public void Build_FoldsIntoOtherGrants_ForMoreThanThreeGrants()
    {
        var grants = Enumerable.Range(0, 5).Select(_ => Grant(100)).ToList();

        var model = Build(grants);

        Assert.Equal(UsageChartModel.MAX_CAPACITY_SERIES + 1, model.Capacity.Count);
        Assert.Equal(UsageChartModel.OTHER_GRANTS, model.Capacity[^1].Label);
        Assert.All(model.Capacity[^1].Data, v => Assert.Equal(200, v));
    }

    [Fact]
    public void Build_DrawsNoCapacityAndFlagsIt_ForAnUnlimitedGrant()
    {
        var model = Build([Grant(null)]);

        Assert.Empty(model.Capacity);
        Assert.True(model.HasUnlimitedGrant);
    }

    [Fact]
    public void Build_StepsCapacityDown_ForAGrantThatExpiresMidRange()
    {
        var grant = Grant(100);
        grant.ValidFromUtc = Start.AddDays(-10);
        grant.ValidToUtc = Start.AddDays(1).AddHours(12);

        var model = Build([grant]);

        Assert.Equal([100L, 100L, 0L], model.Capacity[0].Data);
    }

    [Fact]
    public void Build_LeavesOutGrants_ForAnotherUnitInTheFilter()
    {
        var model = Build(
            [Grant(100)],
            new UsageFilter(
                Start,
                Start.AddDays(2),
                Units: [Corely.Billing.Usage.UsageUnit.From("byte")]
            )
        );

        Assert.Empty(model.Capacity);
    }

    private static UsageChartModel Build(
        IReadOnlyList<Corely.Billing.Grants.Models.Grant> grants,
        UsageFilter? filter = null
    )
    {
        filter ??= new UsageFilter(Start, Start.AddDays(2));
        List<ConsumptionTimeBucketData> buckets =
        [
            new(Start, 5),
            new(Start.AddDays(1), 0),
            new(Start.AddDays(2), 7),
        ];
        return UsageChartModel.Build(buckets, grants, TimeBucket.Day, filter, new StubVocabulary());
    }

    private sealed class StubVocabulary : Corely.Billing.Usage.IUsageVocabulary
    {
        public IReadOnlyList<Corely.Billing.Usage.UsageOperationDefinition> Operations => [];
        public IReadOnlyList<Corely.Billing.Usage.UsageUnitDefinition> Units => [];

        public bool Knows(Corely.Billing.Usage.UsageOperation operation) => true;

        public bool Knows(Corely.Billing.Usage.UsageUnit unit) => true;

        public string DisplayName(Corely.Billing.Usage.UsageOperation operation) => operation.Value;

        public string DisplayName(Corely.Billing.Usage.UsageUnit unit) => unit.Value;
    }
}
