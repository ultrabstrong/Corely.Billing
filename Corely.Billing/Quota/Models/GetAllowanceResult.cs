using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

public record GetAllowanceResult(
    QuotaReadResultCode ResultCode,
    string Message,
    long? Total,
    IReadOnlyList<Grant> Grants
);
