using Corely.Billing.Usage;

namespace Corely.Billing.Quota.Models;

/// <param name="ActualQuantity">What the work turned out to cost.</param>
public record SettleQuotaRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    long ActualQuantity
);
