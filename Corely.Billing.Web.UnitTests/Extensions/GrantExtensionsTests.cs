using Corely.Billing.Usage;
using Corely.Billing.Web.Components;
using Corely.Billing.Web.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class GrantExtensionsTests : BillingWebTestContext
{
    [Theory]
    [InlineData(5, 40, GrantStatus.Upcoming)]
    [InlineData(-5, 40, GrantStatus.Active)]
    [InlineData(0, 40, GrantStatus.Active)]
    [InlineData(-40, 0, GrantStatus.Active)]
    [InlineData(-40, -5, GrantStatus.Expired)]
    public void Status_FollowsTheWindow_ForNow(int fromDays, int toDays, GrantStatus expected) =>
        Assert.Equal(expected, Grant(100, fromDays, toDays).Status(Now));

    [Theory]
    [InlineData(3, 40, "Starts in 3 days")]
    [InlineData(1, 40, "Starts tomorrow")]
    [InlineData(-5, 30, "Expires in 30 days")]
    [InlineData(-40, -1, "Expired yesterday")]
    [InlineData(-400, -91, "Expired 91 days ago")]
    public void WhenText_SaysWhatComesNext_ForEachStatus(
        int fromDays,
        int toDays,
        string expected
    ) => Assert.Equal(expected, Grant(100, fromDays, toDays).WhenText(Now));

    [Fact]
    public void Allowance_UsesTheUnitsDisplayName_ForALimitedGrant() =>
        Assert.Equal("1,500 pages", Grant(1_500).Allowance(Vocabulary));

    [Fact]
    public void Allowance_ReadsUnlimited_ForANullQuantity() =>
        Assert.Equal("Unlimited pages", Grant(null).Allowance(Vocabulary));

    [Fact]
    public void AllowanceAndExpiry_EndsWithTheLastDay_ForAGrant() =>
        Assert.Equal(
            "100 pages to Mar 31, 2026",
            Grant(100, toDays: 30).AllowanceAndExpiry(Vocabulary)
        );

    [Fact]
    public void AllowanceAndWindow_ShowsBothEnds_ForAGrant() =>
        Assert.Equal(
            "100 pages, Feb 19, 2026 – Mar 31, 2026",
            Grant(100, fromDays: -10, toDays: 30).AllowanceAndWindow(Vocabulary)
        );

    private IUsageVocabulary Vocabulary => Services.GetRequiredService<IUsageVocabulary>();
}
