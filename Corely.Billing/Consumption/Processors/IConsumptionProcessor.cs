using Corely.Billing.Consumption.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Processors;

internal interface IConsumptionProcessor
{
    Task<ReserveConsumptionResult> ReserveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    );

    Task<ResolveConsumptionResult> SettleAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        CancellationToken ct = default
    );

    Task<ResolveConsumptionResult> ReleaseAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );

    Task<List<ConsumptionEvent>?> ListOutstandingReservationsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );
}
