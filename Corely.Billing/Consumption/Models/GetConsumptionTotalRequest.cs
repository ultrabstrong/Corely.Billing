using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Models;

public record GetConsumptionTotalRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Provider = null
);
