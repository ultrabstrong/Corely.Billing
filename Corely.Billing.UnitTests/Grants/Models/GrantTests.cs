using Corely.Billing.Grants.Constants;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.UnitTests.Grants.Models;

public class GrantTests
{
    [Theory]
    [InlineData(GrantKind.Consumable, null, GrantType.Unlimited)]
    [InlineData(GrantKind.Consumable, 500L, GrantType.Balance)]
    [InlineData(GrantKind.Capacity, null, GrantType.UnlimitedCapacity)]
    [InlineData(GrantKind.Capacity, 7L, GrantType.Capacity)]
    [InlineData(GrantKind.Allowance, null, GrantType.UnlimitedAllowance)]
    [InlineData(GrantKind.Allowance, 60L, GrantType.Allowance)]
    [InlineData(GrantKind.Feature, null, GrantType.Feature)]
    public void GetGrantType_ReadsTheKindAndQuantity_ForEachCombination(
        GrantKind kind,
        long? quantity,
        GrantType expected
    ) => Assert.Equal(expected, new Grant { Kind = kind, Quantity = quantity }.GetGrantType());

    [Theory]
    [InlineData(500L, 120L, 380L)]
    [InlineData(100L, 130L, 0L)]
    [InlineData(null, 40L, null)]
    public void WithUse_SetsUsedAndWhatIsLeft_ForEachQuantity(
        long? quantity,
        long used,
        long? remaining
    )
    {
        var grant = new Grant { Kind = GrantKind.Consumable, Quantity = quantity };

        grant.WithUse(used);

        Assert.Equal(used, grant.Used);
        Assert.Equal(remaining, grant.Remaining);
    }

    [Fact]
    public void NeverExpires_IsTrue_ForTheNeverExpiresDate() =>
        Assert.True(new Grant { ValidToUtc = GrantConstants.NEVER_EXPIRES_UTC }.NeverExpires());

    [Fact]
    public void NeverExpires_IsFalse_ForAnyEarlierDate() =>
        Assert.False(
            new Grant
            {
                ValidToUtc = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            }.NeverExpires()
        );
}
