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
    [InlineData(5, 40)]
    [InlineData(-5, 40)]
    [InlineData(-40, -5)]
    public void Status_IsDisabled_ForADisabledGrantInAnyWindow(int fromDays, int toDays)
    {
        var grant = Grant(100, fromDays, toDays);
        grant.Disabled = true;

        Assert.Equal(GrantStatus.Disabled, grant.Status(Now));
    }

    [Fact]
    public void WhenText_StillFollowsTheWindow_ForADisabledGrant()
    {
        var grant = Grant(100, -5, 30);
        grant.Disabled = true;

        Assert.Equal("Expires in 30 days", grant.WhenText(Now));
    }

    [Fact]
    public void ToCsvRow_ReportsDisabled_ForADisabledGrant()
    {
        var grant = Grant(100, -5, 30);
        grant.Disabled = true;

        Assert.Equal("disabled", grant.ToCsvRow(Vocabulary, Now).Split(',')[11]);
    }

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
    public void Headline_UsesTheUnitsDisplayName_ForALimitedGrant() =>
        Assert.Equal("1,500 pages", Grant(1_500).Headline(Vocabulary));

    [Fact]
    public void Headline_ReadsUnlimited_ForANullQuantity() =>
        Assert.Equal("Unlimited pages", Grant(null).Headline(Vocabulary));

    [Fact]
    public void Headline_IsTheOperationsName_ForAFeature() =>
        Assert.Equal("SFTP access", Feature().Headline(Vocabulary));

    [Fact]
    public void HeadlineAndExpiry_EndsWithTheLastDay_ForAGrant() =>
        Assert.Equal(
            "100 pages to Mar 31, 2026",
            Grant(100, toDays: 30).HeadlineAndExpiry(Vocabulary)
        );

    [Fact]
    public void HeadlineAndWindow_ShowsBothEnds_ForAGrant() =>
        Assert.Equal(
            "100 pages, Feb 19, 2026 to Mar 31, 2026",
            Grant(100, fromDays: -10, toDays: 30).HeadlineAndWindow(Vocabulary)
        );

    [Fact]
    public void StateText_SaysWhatWasUsed_ForAnUnlimitedBalance() =>
        Assert.Equal(
            "Unlimited, 1,240 pages used",
            Grant(null, used: 1_240).StateText(Vocabulary, Now)
        );

    [Fact]
    public void StateText_CountsTheSeatsHeld_ForSeats() =>
        Assert.Equal("2 of 7 users in use", Capacity(7, inUse: 2).StateText(Vocabulary, Now));

    [Fact]
    public void StateText_CountsTheSeatsHeld_ForUnlimitedSeats() =>
        Assert.Equal("Unlimited, 12 in use", Capacity(null, inUse: 12).StateText(Vocabulary, Now));

    [Fact]
    public void StateText_ReadsAsAnAmountAdded_ForAnAllowance() =>
        Assert.Equal(
            "+60 days, until 31 March 2026",
            Retained(60, toDays: 30).StateText(Vocabulary, Now)
        );

    [Fact]
    public void StateText_ReadsAsUnlimitedDays_ForAnUnlimitedAllowance() =>
        Assert.Equal(
            "Unlimited days, until 31 March 2026",
            Retained(null, toDays: 30).StateText(Vocabulary, Now)
        );

    [Fact]
    public void StateText_SaysEnabledUntilItsEnd_ForAnActiveFeature() =>
        Assert.Equal("Enabled, until 31 March 2026", Feature().StateText(Vocabulary, Now));

    [Fact]
    public void StateText_SaysDisabled_ForADisabledFeature() =>
        Assert.Equal("Disabled", Feature(disabled: true).StateText(Vocabulary, Now));

    [Fact]
    public void StateText_SaysNoEndDate_ForAnAllowanceThatNeverExpires()
    {
        var grant = Retained(60);
        grant.ValidToUtc = Corely.Billing.Grants.Constants.GrantConstants.NEVER_EXPIRES_UTC;

        Assert.Equal("+60 days, with no end date", grant.StateText(Vocabulary, Now));
        Assert.Equal("Never expires", grant.WhenText(Now));
    }

    private IUsageVocabulary Vocabulary => Services.GetRequiredService<IUsageVocabulary>();

    [Fact]
    public void ToCsvRow_ReportsKindTypeAndBalance_ForAnOverdrawnGrant()
    {
        var cells = Grant(100, -5, 30, used: 130).ToCsvRow(Vocabulary, Now).Split(',');

        Assert.Equal(GrantExtensions.CSV_HEADER.Split(',').Length, cells.Length);
        Assert.Equal(
            ["consumable", "balance", "100", "false", "active", "130", "0", ""],
            [cells[3], cells[4], cells[7], cells[8], cells[11], cells[12], cells[13], cells[14]]
        );
    }

    [Fact]
    public void ToCsvRow_LeavesQuantityAndRemainingEmpty_ForAnUnlimitedGrant()
    {
        var cells = Grant(null, used: 7).ToCsvRow(Vocabulary, Now).Split(',');

        Assert.Equal(["", "true", "7", ""], [cells[7], cells[8], cells[12], cells[13]]);
    }

    [Fact]
    public void ToCsvRow_ReportsTheSeatsHeld_ForACapacityGrant()
    {
        var cells = Capacity(7, inUse: 3).ToCsvRow(Vocabulary, Now).Split(',');

        Assert.Equal(
            ["capacity", "seats", "7", "", "3"],
            [cells[3], cells[4], cells[7], cells[12], cells[14]]
        );
    }

    [Fact]
    public void ToCsvRow_LeavesUnitAndUnlimitedBlankAndFalse_ForAFeature()
    {
        var cells = Feature().ToCsvRow(Vocabulary, Now).Split(',');

        Assert.Equal(
            ["feature", "feature", "", "", "", "false"],
            [cells[3], cells[4], cells[5], cells[6], cells[7], cells[8]]
        );
    }
}
