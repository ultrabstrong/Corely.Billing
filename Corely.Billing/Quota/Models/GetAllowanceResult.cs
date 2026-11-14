using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

public record GetAllowanceResult(
    QuotaReadResultCode ResultCode,
    string Message,
    decimal? Total,
    IReadOnlyList<Grant> Grants
);
