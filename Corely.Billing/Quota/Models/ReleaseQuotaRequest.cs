using Corely.Billing.Usage;

namespace Corely.Billing.Quota.Models;

public record ReleaseQuotaRequest(Guid AccountId, UsageOperation Operation, UsageUnit Unit);
