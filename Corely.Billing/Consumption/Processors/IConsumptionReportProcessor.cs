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

    Task<int> CountAbandonedReservationsAsync(Guid accountId, CancellationToken ct = default);
}
