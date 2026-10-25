using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

public record GetCapacityResult(
    QuotaReadResultCode ResultCode,
    string Message,
    long? Granted,
    long InUse,
    IReadOnlyList<Grant> Grants
)
{
    public long? Free => Granted is { } granted ? Math.Max(0, granted - InUse) : null;
}
