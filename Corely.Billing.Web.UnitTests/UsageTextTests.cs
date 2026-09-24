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
}
