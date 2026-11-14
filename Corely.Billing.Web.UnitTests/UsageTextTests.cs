namespace Corely.Billing.Web.UnitTests;

public class UsageTextTests
{
    [Theory]
    [InlineData(1L, "1 page")]
    [InlineData(0L, "0 pages")]
    [InlineData(1500L, "1,500 pages")]
    [InlineData(null, "Unlimited pages")]
    public void Count_Pluralizes_ForTheQuantity(long? count, string expected) =>
        Assert.Equal(expected, UsageText.Count(count, "page"));

    [Theory]
    [InlineData("4.500000", "4.5")]
    [InlineData("0.000001", "0.000001")]
    [InlineData("1234.5", "1,234.5")]
    [InlineData("100.000000", "100")]
    public void Number_ShowsEveryDigitWithoutTrailingZeros_ForADecimal(
        string value,
        string expected
    ) => Assert.Equal(expected, UsageText.Number(decimal.Parse(value)));

    [Fact]
    public void Count_ShowsTheFraction_ForAFractionalQuantity() =>
        Assert.Equal("3.333333 credits", UsageText.Count(3.333333m, "credit"));
}
