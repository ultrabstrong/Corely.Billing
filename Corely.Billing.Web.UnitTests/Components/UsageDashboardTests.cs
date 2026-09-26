using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageDashboardTests : BillingWebTestContext
{
    private int _inFlight;
    private int _mostInFlight;

    private async Task<T> TrackAsync<T>(T result)
    {
        var now = Interlocked.Increment(ref _inFlight);
        _mostInFlight = Math.Max(_mostInFlight, now);
        await Task.Delay(20);
        Interlocked.Decrement(ref _inFlight);
        return result;
    }

    [Fact]
    public void Render_NeverOverlapsTwoBillingCalls_ForTheChartAndTableLoadingTogether()
    {
        Grants
            .Setup(g =>
                g.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                TrackAsync(
                    new RetrieveListResult<Grant>(
                        RetrieveResultCode.Success,
                        string.Empty,
                        PagedResult<Grant>.Empty()
                    )
                )
            );
        Consumption
            .Setup(c =>
                c.GetConsumptionTimeSeriesAsync(
                    It.IsAny<GetConsumptionTimeSeriesRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                TrackAsync(
                    new RetrieveSingleResult<List<ConsumptionTimeBucketData>>(
                        RetrieveResultCode.Success,
                        string.Empty,
                        []
                    )
                )
            );
        Consumption
            .Setup(c =>
                c.ListConsumptionEventsAsync(
                    It.IsAny<ListConsumptionEventsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                TrackAsync(
                    new RetrieveListResult<ConsumptionEvent>(
                        RetrieveResultCode.Success,
                        string.Empty,
                        PagedResult<ConsumptionEvent>.Empty()
                    )
                )
            );
        Consumption
            .Setup(c => c.ListProvidersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
                TrackAsync(
                    new RetrieveSingleResult<List<string>>(RetrieveResultCode.Success, "", [])
                )
            );
        Consumption
            .Setup(c =>
                c.GetEarliestConsumptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .Returns(() =>
                TrackAsync(
                    new RetrieveSingleResult<DateTime?>(RetrieveResultCode.Success, "", null)
                )
            );

        var dashboard = Render<UsageDashboard>(p => p.Add(c => c.AccountId, AccountId));

        dashboard.WaitForAssertion(
            () => Assert.Contains("Nothing was used in this range.", dashboard.Markup),
            TimeSpan.FromSeconds(5)
        );
        Assert.Equal(1, _mostInFlight);
    }
}
