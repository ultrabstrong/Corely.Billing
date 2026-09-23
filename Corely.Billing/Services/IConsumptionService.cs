using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;

namespace Corely.Billing.Services;

public interface IConsumptionService
{
    Task<RetrieveSingleResult<long>> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<List<GrantTotalConsumptions>>> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<List<ConsumptionTimeBucketData>>> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    );

    Task<RetrieveListResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<List<string>>> ListProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<DateTime?>> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<int>> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    );
}
