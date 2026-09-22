using Corely.Billing;

namespace Corely.Billing.Consumption.Models;

public record ConsumptionEventSummary(
    Guid ConsumptionId,
    long Quantity,
    UsageUnit Unit,
    UsageOperation Operation,
    string Provider,
    DateTime UtcTimestamp,
    Guid GrantId,
    /// <summary>How the reservation resolved; null while it is still outstanding.</summary>
    ConsumptionOutcome? Outcome,
    Guid? UserId,
    IReadOnlyDictionary<string, string>? Tags
);
