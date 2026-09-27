using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;
using Corely.Billing.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageExporterTests : BillingWebTestContext
{
    private readonly List<ListConsumptionEventsRequest> _requests = [];

    [Fact]
    public async Task DownloadEventsAsync_StopsAtTheCapAndSaysSo_ForAFilterMatchingMore()
    {
        HaveEvents(matched: 134_210);

        var outcome = await Exporter()
            .DownloadEventsAsync(AccountId, new UsageFilter(Now.AddDays(-7), Now));

        Assert.False(outcome.IsError);
        Assert.Equal(UsageExport.CappedNotice(UsageExport.MAX_EVENTS, 134_210), outcome.Message);
        Assert.Equal(UsageExport.MAX_EVENTS, _requests.Sum(r => r.Take));
        Assert.Single(JSInterop.Invocations, i => i.Identifier == "download");
    }

    [Fact]
    public async Task DownloadEventsAsync_ReadsEveryRowWithoutANotice_ForAFilterUnderTheCap()
    {
        HaveEvents(matched: 2_500);

        var outcome = await Exporter()
            .DownloadEventsAsync(AccountId, new UsageFilter(Now.AddDays(-7), Now));

        Assert.Equal(ExportOutcome.Done, outcome);
        Assert.Equal([0, 1_000, 2_000], _requests.Select(r => r.Skip));
    }

    [Fact]
    public async Task DownloadEventsAsync_DownloadsNothing_ForAnUnauthorizedRead()
    {
        Consumption
            .Setup(c =>
                c.ListConsumptionEventsAsync(
                    It.IsAny<ListConsumptionEventsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<ConsumptionEvent>(
                    RetrieveResultCode.UnauthorizedError,
                    "no",
                    null
                )
            );

        var outcome = await Exporter()
            .DownloadEventsAsync(AccountId, new UsageFilter(Now.AddDays(-7), Now));

        Assert.True(outcome.IsError);
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "download");
    }

    private UsageExporter Exporter() => Services.GetRequiredService<UsageExporter>();

    private void HaveEvents(int matched) =>
        Consumption
            .Setup(c =>
                c.ListConsumptionEventsAsync(
                    It.IsAny<ListConsumptionEventsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ListConsumptionEventsRequest request, CancellationToken _) =>
                {
                    _requests.Add(request);
                    var count = Math.Max(0, Math.Min(request.Take, matched - request.Skip));
                    List<ConsumptionEvent> items =
                    [
                        .. Enumerable
                            .Range(0, count)
                            .Select(_ => new ConsumptionEvent
                            {
                                ConsumptionId = Guid.CreateVersion7(),
                                AccountId = AccountId,
                                GrantId = Guid.CreateVersion7(),
                                Operation = Extraction,
                                Unit = Page,
                                Quantity = 1,
                                Provider = "p",
                                IdempotencyKey = "k",
                                UtcTimestamp = Now,
                            }),
                    ];
                    return new RetrieveListResult<ConsumptionEvent>(
                        RetrieveResultCode.Success,
                        string.Empty,
                        new PagedResult<ConsumptionEvent>(items, matched, request.Skip, false)
                    );
                }
            );
}
