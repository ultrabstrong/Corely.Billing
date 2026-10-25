using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

public record GetBalanceResult(
    QuotaReadResultCode ResultCode,
    string Message,
    long? Remaining,
    IReadOnlyList<Grant> Grants
)
{
    public bool HasAny => Remaining is null or > 0 && Grants.Count > 0;
}
