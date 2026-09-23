using Corely.Billing.Usage;

namespace Corely.Billing.Quota.Models;

public record SettleQuotaRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    long ActualQuantity
);
