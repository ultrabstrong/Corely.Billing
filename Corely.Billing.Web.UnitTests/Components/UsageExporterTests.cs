using System.Text;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

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

    [Fact]
    public async Task DownloadAllAsync_DownloadsWithoutReadingGrants_ForAnExportWithoutGrants()
    {
        HaveEvents(matched: 3);

        var outcome = await Exporter()
            .DownloadAllAsync(AccountId, new UsageFilter(Now.AddDays(-7), Now), null, null, false);

        Assert.Equal(ExportOutcome.Done, outcome);
        Assert.Single(JSInterop.Invocations, i => i.Identifier == "download");
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
    public async Task DownloadGrantsAsync_WritesEachGrantsState_ForGrantsOfEveryKind()
    {
        var balance = Grant(5_000, used: 1_200);
        HaveGrants(balance, Retained(60), Feature(), Capacity(7, inUse: 2));

        await Exporter().DownloadGrantsAsync(AccountId);

        var rows = DownloadedCsv().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5, rows.Length);
        var first = rows[1].Split(',');
        Assert.Equal(balance.GrantId.ToString(), first[0]);
        Assert.Equal(["1200", "3800"], first[12..14]);
    }

    private string DownloadedCsv()
    {
        var download = Assert.Single(JSInterop.Invocations, i => i.Identifier == "download");
        var stream = Assert.IsType<DotNetStreamReference>(download.Arguments[1]).Stream;
        return Encoding
            .UTF8.GetString(Assert.IsType<MemoryStream>(stream).ToArray())
            .TrimStart('﻿');
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
