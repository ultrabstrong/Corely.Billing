using Corely.Billing.Grants.Models;
using Corely.Billing.Prices.Models;

namespace Corely.Billing.Quota.Models;

public record GetBalanceResult(
    QuotaReadResultCode ResultCode,
    string Message,
    decimal? Remaining,
    IReadOnlyList<Grant> Grants
)
{
    public bool HasAny => Remaining is null or > 0 && Grants.Count > 0;

    public Money? RemainingValue()
    {
        var holding = Grants.Where(g => g.Remaining is > 0).ToList();
        if (
            Remaining is null
            || holding.Count == 0
            || holding.Any(g => g.UnitPrice is null)
            || holding.Select(g => g.UnitPrice!.Value.Currency).Distinct().Count() > 1
        )
        {
            return null;
        }
        return new Money(
            holding.Sum(g => g.Remaining!.Value * g.UnitPrice!.Value.Amount),
            holding[0].UnitPrice!.Value.Currency
        );
    }
}
