using Corely.Billing.Grants.Models;
using Corely.Billing.Prices.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.UnitTests.Quota.Models;

public class GetBalanceResultTests
{
    private static Grant Holding(decimal remaining, Money? price) =>
        new()
        {
            Quantity = remaining,
            Remaining = remaining,
            UnitPrice = price,
        };

    private static GetBalanceResult Balance(params Grant[] grants) =>
        new(QuotaReadResultCode.Success, string.Empty, grants.Sum(g => g.Remaining ?? 0), grants);

    [Fact]
    public void RemainingValue_AddsEachGrantAtItsOwnPrice_ForPricedGrants() =>
        Assert.Equal(
            Money.Usd(2.5m),
            Balance(Holding(1000, Money.Usd(0.001m)), Holding(750, Money.Usd(0.002m)))
                .RemainingValue()
        );

    [Fact]
    public void RemainingValue_IsNull_ForAnUnpricedGrantAmongThem() =>
        Assert.Null(Balance(Holding(1000, Money.Usd(0.001m)), Holding(750, null)).RemainingValue());

    [Fact]
    public void RemainingValue_IgnoresAnUnpricedGrant_ForOneWithNothingLeft() =>
        Assert.Equal(
            Money.Usd(1),
            Balance(Holding(1000, Money.Usd(0.001m)), Holding(0, null)).RemainingValue()
        );
}
