using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;

namespace Corely.Billing.Consumption.Processors;

internal interface IConsumptionReportProcessor
{
    Task<long> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    );

    Task<List<GrantTotalConsumptions>> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    );

    Task<List<ConsumptionTimeBucketData>> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    );

    Task<PagedResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    );

    Task<List<string>> ListProvidersAsync(Guid accountId, CancellationToken ct = default);

    Task<DateTime?> GetEarliestConsumptionAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Reservations that were never resolved and have passed their TTL.
    /// </summary>
    /// <remarks>
    /// Every one of these is work that died between holding quota and settling it. They stop
    /// counting against a grant on their own, so nothing breaks -- which is exactly why they need
    /// counting: a rising number is the only sign that something upstream is failing silently.
    /// </remarks>
    Task<int> CountAbandonedReservationsAsync(Guid accountId, CancellationToken ct = default);
}
