using Corely.Billing;
using Corely.Billing.Consumption.Models;

namespace Corely.Billing.Consumption.Services;

public interface IConsumptionReader
{
    Task<GetConsumptionTotalResult> GetConsumptionTotalAsync(
        Guid accountId,
        UsageUnit unit,
        UsageOperation operation,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? provider = null,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Reservations that were never resolved and have passed their TTL.
    /// </summary>
    /// <remarks>
    /// Every one of these is a step that died between holding quota and settling it. They stop
    /// counting against a grant on their own, so nothing breaks -- which is exactly why they need
    /// counting: a rising number is the only sign that something upstream is failing silently.
    /// </remarks>
    Task<int> CountAbandonedReservationsAsync(Guid accountId, CancellationToken ct = default);

    Task<GetOutstandingReservationsResult> GetOutstandingReservationsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );

    Task<GetGrantConsumptionTotalsResult> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        Guid[] grantIds,
        CancellationToken ct = default
    );

    Task<GetConsumptionTimeSeriesResult> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    );

    Task<ListConsumptionEventsResult> ListConsumptionEventsAsync(
        Guid accountId,
        int skip,
        int take,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        IReadOnlyList<UsageUnit>? units = null,
        IReadOnlyList<UsageOperation>? operations = null,
        IReadOnlyList<string>? providers = null,
        IReadOnlyList<Guid>? grantIds = null,
        string? sortColumn = null,
        bool sortDescending = false,
        CancellationToken ct = default
    );

    Task<List<string>> GetDistinctProvidersAsync(Guid accountId, CancellationToken ct = default);

    Task<DateTime?> GetEarliestEventDateAsync(Guid accountId, CancellationToken ct = default);
}
