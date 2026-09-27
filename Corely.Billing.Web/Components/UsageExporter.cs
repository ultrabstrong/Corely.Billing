using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Corely.Billing.Web.Components;

internal sealed record ExportOutcome(string? Message, bool IsError)
{
    public static ExportOutcome Done { get; } = new(null, false);
}

// Reads what an export holds and hands the file to the browser. Callers run it through the
// component's call gate, like any other Billing read.
internal sealed class UsageExporter(
    IConsumptionService consumptionService,
    IGrantService grantService,
    IUsageVocabulary vocabulary,
    IOptions<ReservationOptions> reservationOptions,
    TimeProvider timeProvider,
    IJSRuntime js
) : IAsyncDisposable
{
    private const string MODULE_PATH = "./_content/Corely.Billing.Web/billing-download.js";
    private const int PAGE_SIZE = 1_000;

    private IJSObjectReference? _module;

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private TimeSpan Ttl => reservationOptions.Value.ReservationTtl;

    public async Task<ExportOutcome> DownloadEventsAsync(Guid accountId, UsageFilter filter)
    {
        var events = await ReadEventsAsync(accountId, filter);
        if (events.Error is not null)
            return new(events.Error, true);

        await DownloadAsync(UsageExport.EVENTS_FILE, UsageExport.CsvBytes(events.Csv));
        return events.Included < events.Matched
            ? new(UsageExport.CappedNotice(events.Included, events.Matched), false)
            : ExportOutcome.Done;
    }

    public async Task<ExportOutcome> DownloadGrantsAsync(Guid accountId)
    {
        var grants = await ReadGrantsAsync(accountId);
        if (grants.Error is not null)
            return new(grants.Error, true);

        await DownloadAsync(UsageExport.GRANTS_FILE, UsageExport.CsvBytes(grants.Csv));
        return ExportOutcome.Done;
    }

    public async Task<ExportOutcome> DownloadChartAsync(UsageChartModel model)
    {
        await DownloadAsync(UsageExport.CHART_FILE, UsageExport.CsvBytes(model.ToCsv()));
        return ExportOutcome.Done;
    }

    public async Task<ExportOutcome> DownloadAllAsync(
        Guid accountId,
        UsageFilter filter,
        UsageChartModel? chart,
        string? chartCaption
    )
    {
        var events = await ReadEventsAsync(accountId, filter);
        if (events.Error is not null)
            return new(events.Error, true);
        var grants = await ReadGrantsAsync(accountId);
        if (grants.Error is not null)
            return new(grants.Error, true);

        var export = new UsageExport(
            accountId,
            filter,
            Now,
            Ttl,
            events.Csv,
            events.Included,
            events.Matched,
            grants.Csv,
            chart is { IsEmpty: false } ? chart.ToCsv() : null,
            chartCaption
        );
        await DownloadAsync(export.FileName, export.ToZip(vocabulary));
        return export.IsCapped
            ? new(UsageExport.CappedNotice(events.Included, events.Matched), false)
            : ExportOutcome.Done;
    }

    private async Task<(string Csv, int Included, int Matched, string? Error)> ReadEventsAsync(
        Guid accountId,
        UsageFilter filter
    )
    {
        var liveFromUtc = Now - Ttl;
        var lines = new List<string> { ConsumptionEventExtensions.CSV_HEADER };
        var matched = 0;

        for (var skip = 0; skip < UsageExport.MAX_EVENTS; skip += PAGE_SIZE)
        {
            var page = await consumptionService.ListConsumptionEventsAsync(
                new ListConsumptionEventsRequest(
                    accountId,
                    filter.FromUtc,
                    filter.ToUtc,
                    filter.Units,
                    filter.Operations,
                    filter.Providers,
                    filter.GrantIds,
                    Skip: skip,
                    Take: Math.Min(PAGE_SIZE, UsageExport.MAX_EVENTS - skip)
                )
            );
            if (page.ResultCode != RetrieveResultCode.Success)
                return (
                    string.Empty,
                    0,
                    0,
                    page.ResultCode.ErrorMessage("export usage", page.Message)
                );

            var items = page.Data?.Items ?? [];
            matched = page.Data?.TotalCount ?? 0;
            lines.AddRange(items.Select(e => e.ToCsvRow(vocabulary, liveFromUtc)));
            if (items.Count == 0 || skip + items.Count >= matched)
                break;
        }

        return (Join(lines), lines.Count - 1, matched, null);
    }

    private async Task<(string Csv, string? Error)> ReadGrantsAsync(Guid accountId)
    {
        var result = await grantService.ListGrantsAsync(
            new ListGrantsRequest(accountId, Take: int.MaxValue)
        );
        if (result.ResultCode != RetrieveResultCode.Success)
            return (string.Empty, result.ResultCode.ErrorMessage("export grants", result.Message));

        List<Grant> grants = result.Data?.Items ?? [];
        var used = new Dictionary<Guid, long>();
        if (grants.Count > 0)
        {
            var totals = await consumptionService.GetGrantConsumptionTotalsAsync(
                accountId,
                [.. grants.Select(g => g.GrantId)]
            );
            used = (totals.Item ?? []).ToDictionary(t => t.GrantId, t => t.TotalConsumedQuantity);
        }

        var now = Now;
        return (
            Join([
                GrantExtensions.CSV_HEADER,
                .. grants
                    .OrderBy(g => g.ValidFromUtc)
                    .Select(g => g.ToCsvRow(vocabulary, used.GetValueOrDefault(g.GrantId), now)),
            ]),
            null
        );
    }

    private async Task DownloadAsync(string fileName, byte[] content)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", MODULE_PATH);
        using var stream = new DotNetStreamReference(new MemoryStream(content));
        await _module.InvokeVoidAsync("download", fileName, stream);
    }

    private static string Join(IEnumerable<string> lines) => string.Join("\r\n", lines) + "\r\n";

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;
        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
