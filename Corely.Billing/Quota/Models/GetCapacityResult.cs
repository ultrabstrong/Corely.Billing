using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

public record GetCapacityResult(
    QuotaReadResultCode ResultCode,
    string Message,
    decimal? Granted,
    decimal InUse,
    IReadOnlyList<Grant> Grants
)
{
    public decimal? Free => Granted is { } granted ? Math.Max(0, granted - InUse) : null;
}
