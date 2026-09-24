using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class GrantBalanceTests
{
    [Theory]
    [InlineData(100L, 95L, true, false)]
    [InlineData(100L, 50L, false, false)]
    [InlineData(100L, 120L, false, true)]
    [InlineData(null, 5000L, false, false)]
    public void State_ReadsLowOrOverdrawn_ForUsedAgainstQuantity(
        long? quantity,
        long used,
        bool runningLow,
        bool overdrawn
    )
    {
        var balance = new GrantBalance(quantity, used);

        Assert.Equal((runningLow, overdrawn), (balance.IsRunningLow, balance.IsOverdrawn));
    }

    [Fact]
    public void UsedRatio_StaysWithinTheBar_ForAnOverdrawnGrant() =>
        Assert.Equal(1, new GrantBalance(10, 25).UsedRatio);
}
