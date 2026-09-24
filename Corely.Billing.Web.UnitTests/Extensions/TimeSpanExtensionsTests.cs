using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class TimeSpanExtensionsTests
{
    [Theory]
    [InlineData(-0.5, "today")]
    [InlineData(0.5, "today")]
    [InlineData(1.2, "tomorrow")]
    [InlineData(2, "in 2 days")]
    [InlineData(1_200, "in 1,200 days")]
    public void AheadText_CountsWholeDays_ForASpanAhead(double days, string expected) =>
        Assert.Equal(expected, TimeSpan.FromDays(days).AheadText());

    [Theory]
    [InlineData(0.5, "today")]
    [InlineData(1.9, "yesterday")]
    [InlineData(91, "91 days ago")]
    public void AgoText_CountsWholeDays_ForASpanBehind(double days, string expected) =>
        Assert.Equal(expected, TimeSpan.FromDays(days).AgoText());
}
