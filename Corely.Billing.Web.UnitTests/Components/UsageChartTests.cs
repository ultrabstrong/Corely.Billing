using Corely.Billing.Consumption.Enums;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageChartTests : BillingWebTestContext
{
    private static readonly UsageUnit Token = UsageUnit.From("token");
    private readonly List<GetConsumptionTimeSeriesRequest> _requests = [];

    public UsageChartTests()
    {
        Consumption
            .Setup(c =>
                c.GetConsumptionTimeSeriesAsync(
                    It.IsAny<GetConsumptionTimeSeriesRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (GetConsumptionTimeSeriesRequest request, CancellationToken _) =>
                {
                    _requests.Add(request);
                    List<ConsumptionSeries> series = request.By switch
                    {
                        ConsumptionDimension.Unit =>
                        [
                            Series(Token.Value, 900),
                            Series(Page.Value, 4),
                        ],
                        ConsumptionDimension.Operation => [Series(Extraction.Value, 4)],
                        ConsumptionDimension.Grant => [],
                        _ => [Series(null, 4)],
                    };
                    return new RetrieveSingleResult<List<ConsumptionSeries>>(
                        RetrieveResultCode.Success,
                        string.Empty,
                        series
                    );
                }
            );
    }

    [Fact]
    public void Render_PlotsOneUnitAndOffersTheOthers_ForARangeWithTwoUnits()
    {
        var chart = Render();

        var plotted = _requests.Where(r => r.By != ConsumptionDimension.Unit).ToList();
        Assert.NotEmpty(plotted);
        Assert.All(plotted, r => Assert.Equal([Page], r.Units));
        Assert.Equal(2, chart.FindAll("select[aria-label='Unit'] option").Count);
    }

    [Fact]
    public void Render_LabelsCapacityByGrant_ForAnOperationBreakdownAndALimitedGrant()
    {
        HaveGrants(Grant(100));

        var chart = Render(UsageChartView.Used, UsageBreakdown.Operation);

        Assert.DoesNotContain("alert-danger", chart.Markup);
        Assert.Contains("Live capacity", chart.Markup);
    }

    [Fact]
    public void Render_AsksForTheBreakdown_ForTheStackedView()
    {
        Render(UsageChartView.Stacked, UsageBreakdown.Operation);

        Assert.Contains(_requests, r => r.By == ConsumptionDimension.Operation);
    }

    [Fact]
    public void Render_ReadsEachGrantsWholeHistory_ForTheRemainingView()
    {
        var grant = Grant(100, fromDays: -60, toDays: 30);
        HaveGrants(grant);

        Render(UsageChartView.Remaining);

        var request = Assert.Single(_requests, r => r.By == ConsumptionDimension.Grant);
        Assert.Equal(grant.ValidFromUtc, request.FromUtc);
        Assert.Equal([grant.GrantId], request.GrantIds);
        Assert.Null(request.Providers);
    }

    [Fact]
    public void Render_OffersOnlyViewsWithoutGrants_ForAChartWithoutGrants()
    {
        var chart = Render(UsageChartView.Stacked, showGrants: false);

        Assert.Equal(
            ["Used", "Stacked", "SideBySide", "Share"],
            chart.FindAll("select[aria-label='Chart'] option").Select(o => o.GetAttribute("value"))
        );
        Assert.Equal(
            ["Operation", "Provider"],
            chart
                .FindAll("select[aria-label='Break down by'] option")
                .Select(o => o.GetAttribute("value"))
        );
        Grants.Verify(
            g =>
                g.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Render_FallsBackToUsedByOperation_ForGrantViewsOnAChartWithoutGrants()
    {
        Render(UsageChartView.Remaining, UsageBreakdown.Grant, showGrants: false);

        Assert.DoesNotContain(_requests, r => r.By == ConsumptionDimension.Grant);
        Assert.Contains(_requests, r => r.By is null);
    }

    private IRenderedComponent<UsageChart> Render(
        UsageChartView view = UsageChartView.Used,
        UsageBreakdown by = UsageBreakdown.Operation,
        bool showGrants = true
    ) =>
        Render<UsageChart>(p =>
            p.Add(c => c.AccountId, AccountId)
                .Add(c => c.Filter, new UsageFilter(Now.AddDays(-7), Now))
                .Add(c => c.View, view)
                .Add(c => c.BreakDownBy, by)
                .Add(c => c.ShowGrants, showGrants)
        );

    private static ConsumptionSeries Series(string? key, long total) =>
        new(key, [new ConsumptionTimeBucketData(Now.Date, total)]);
}
