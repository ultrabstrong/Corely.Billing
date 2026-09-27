using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageChartModelTests : BillingWebTestContext
{
    private static readonly DateTime Start = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly UsageFilter ThreeDays = new(Start, Start.AddDays(2).AddHours(23));

    [Theory]
    [InlineData(7, TimeBucket.Day)]
    [InlineData(31, TimeBucket.Day)]
    [InlineData(90, TimeBucket.Week)]
    [InlineData(365, TimeBucket.Month)]
    public void BucketFor_PicksTheBucket_ForTheRangeLength(int days, TimeBucket expected) =>
        Assert.Equal(expected, UsageChartModel.BucketFor(Start, Start.AddDays(days)));

    [Fact]
    public void RelevantGrants_LeavesOutGrants_ForAnotherUnit()
    {
        var other = Grant(100);
        other.Unit = UsageUnit.From("byte");

        var relevant = UsageChartModel.RelevantGrants([Grant(100), other], ThreeDays, Page);

        Assert.Single(relevant);
    }

    [Fact]
    public void Build_FoldsIntoOtherGrants_ForMoreThanThreeGrants()
    {
        var grants = Enumerable.Range(0, 5).Select(_ => Grant(100)).ToList();

        var model = Build(UsageChartView.Used, [Total(5, 0, 7)], grants);

        Assert.Equal(UsageChartModel.MAX_CAPACITY_SERIES + 1, model.Capacity.Count);
        Assert.Equal(UsageChartModel.OTHER_GRANTS, model.Capacity[^1].Label);
        Assert.All(model.Capacity[^1].Data, v => Assert.Equal(200, v));
    }

    [Fact]
    public void Build_DrawsNoCapacityAndFlagsIt_ForAnUnlimitedGrant()
    {
        var model = Build(UsageChartView.Used, [Total(5, 0, 7)], [Grant(null)]);

        Assert.Empty(model.Capacity);
        Assert.True(model.HasUnlimitedGrant);
    }

    [Fact]
    public void Build_StepsCapacityDown_ForAGrantThatExpiresMidRange()
    {
        var grant = Grant(100);
        grant.ValidFromUtc = Start.AddDays(-10);
        grant.ValidToUtc = Start.AddDays(1).AddHours(12);

        var model = Build(UsageChartView.Used, [Total(5, 0, 7)], [grant]);

        Assert.Equal([100L, 100L, 0L], model.Capacity[0].Data);
    }

    [Fact]
    public void Build_PlotsTheTotal_ForTheUsedView()
    {
        var model = Build(UsageChartView.Used, [Total(5, 0, 7)], []);

        var series = Assert.Single(model.Series);
        Assert.Equal([5L, 0L, 7L], series.Data);
        Assert.Equal(12, model.Total);
    }

    [Fact]
    public void Build_FoldsPastFiveSeriesIntoOther_ForTheStackedView()
    {
        var ranked = Enumerable.Range(0, 7).Select(i => Keyed($"k{i}", 7 - i, 0, 1)).ToList();

        var model = Build(UsageChartView.Stacked, ranked, []);

        Assert.Equal(UsageChartModel.MAX_SERIES + 1, model.Series.Count);
        var other = model.Series[^1];
        Assert.True(other.IsOther);
        Assert.Equal([3L, 0L, 2L], other.Data);
    }

    [Fact]
    public void Build_RanksTotalsOverTheWholeRange_ForTheShareView()
    {
        var model = Build(
            UsageChartView.Share,
            [Keyed("big", 5, 5, 5), Keyed("small", 1, 0, 0)],
            []
        );

        Assert.Single(model.Labels);
        Assert.Equal(["big", "small"], model.Series.Select(s => s.Key));
        Assert.Equal([15L], model.Series[0].Data);
        Assert.Empty(model.Capacity);
    }

    [Fact]
    public void Build_OpensAtTheBalanceBeforeTheRange_ForTheRemainingView()
    {
        var grant = Grant(100);
        grant.ValidFromUtc = Start.AddDays(-2);
        grant.ValidToUtc = Start.AddDays(1).AddHours(12);
        var history = new ConsumptionSeries(
            grant.GrantId.ToString(),
            [
                new(Start.AddDays(-2), 30),
                new(Start.AddDays(-1), 20),
                new(Start, 10),
                new(Start.AddDays(1), 90),
                new(Start.AddDays(2), 0),
            ]
        );

        var model = Build(UsageChartView.Remaining, [history], [grant]);

        var total = model.Series[0];
        Assert.Equal(UsageChartModel.ALL_LIVE_GRANTS, total.Key);
        // 100 less 50 before the range, then 10 and 90 more; floored at zero, then gone once expired.
        Assert.Equal([40L, 0L, null], model.Series[1].Data);
        Assert.Equal([40L, 0L, null], total.Data);
    }

    [Fact]
    public void Build_AccumulatesUseAgainstTheAllowance_ForTheBurnUpView()
    {
        var model = Build(
            UsageChartView.BurnUp,
            [Keyed("a", 5, 0, 3), Keyed("b", 0, 0, 4)],
            [Grant(100)]
        );

        Assert.Equal([5L, 5L, 12L], model.Series[0].Data);
        Assert.Equal([100L, 100L, 100L], model.Series[1].Data);
        Assert.Equal(12, model.Total);
    }

    [Fact]
    public void ToCsv_WritesOneRowPerPeriodAndSeries_WithEmptyCellsForGaps()
    {
        var model = new UsageChartModel(
            UsageChartView.Remaining,
            Page,
            "page",
            [Start, Start.AddDays(1)],
            ["Mar 2", "Mar 3"],
            [new ChartSeries("g", "Grant, one", [4, null])],
            [],
            false
        );

        var lines = model.ToCsv().TrimEnd().Split("\r\n");

        Assert.Equal(UsageChartModel.CSV_HEADER, lines[0]);
        Assert.Equal("2026-03-02T00:00:00.000Z,Mar 2,g,\"Grant, one\",4,page", lines[1]);
        Assert.Equal("2026-03-03T00:00:00.000Z,Mar 3,g,\"Grant, one\",,page", lines[2]);
    }

    private static ConsumptionSeries Total(params long[] values) => Keyed(null, values);

    private static ConsumptionSeries Keyed(string? key, params long[] values) =>
        new(key, [.. values.Select((v, i) => new ConsumptionTimeBucketData(Start.AddDays(i), v))]);

    private static UsageChartModel Build(
        UsageChartView view,
        IReadOnlyList<ConsumptionSeries> series,
        IReadOnlyList<Grant> grants
    ) =>
        UsageChartModel.Build(
            view,
            series,
            UsageChartModel.RelevantGrants(grants, ThreeDays, Page),
            TimeBucket.Day,
            ThreeDays,
            Page,
            "page",
            key => key,
            grant => grant.GrantId.ToString()
        );
}
