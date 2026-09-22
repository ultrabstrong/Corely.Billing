using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Models;
using Corely.Common.Extensions;

namespace Corely.Billing.Services;

internal class ConsumptionService(IConsumptionReportProcessor consumptionReportProcessor)
    : IConsumptionService
{
    private readonly IConsumptionReportProcessor _consumptionReportProcessor =
        consumptionReportProcessor.ThrowIfNull(nameof(consumptionReportProcessor));

    public async Task<RetrieveSingleResult<long>> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    ) => Found(await _consumptionReportProcessor.GetConsumptionTotalAsync(request, ct));

    public async Task<
        RetrieveSingleResult<List<GrantTotalConsumptions>>
    > GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    ) =>
        Found(
            await _consumptionReportProcessor.GetGrantConsumptionTotalsAsync(
                accountId,
                grantIds,
                ct
            )
        );

    public async Task<
        RetrieveSingleResult<List<ConsumptionTimeBucketData>>
    > GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    ) => Found(await _consumptionReportProcessor.GetConsumptionTimeSeriesAsync(request, ct));

    public async Task<RetrieveListResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    ) =>
        new(
            RetrieveResultCode.Success,
            string.Empty,
            await _consumptionReportProcessor.ListConsumptionEventsAsync(request, ct)
        );

    public async Task<RetrieveSingleResult<List<string>>> ListProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => Found(await _consumptionReportProcessor.ListProvidersAsync(accountId, ct));

    public async Task<RetrieveSingleResult<DateTime?>> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => Found(await _consumptionReportProcessor.GetEarliestConsumptionAsync(accountId, ct));

    public async Task<RetrieveSingleResult<int>> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => Found(await _consumptionReportProcessor.CountAbandonedReservationsAsync(accountId, ct));

    private static RetrieveSingleResult<T> Found<T>(T item) =>
        new(RetrieveResultCode.Success, string.Empty, item);
}
