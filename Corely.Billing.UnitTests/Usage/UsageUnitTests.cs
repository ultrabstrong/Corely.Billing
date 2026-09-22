using Corely.Billing.Usage;

namespace Corely.Billing.UnitTests.Usage;

public class UsageUnitTests
{
    [Theory]
    [InlineData("page")]
    [InlineData("byte")]
    [InlineData("api_call")]
    public void From_KeepsTheValue_ForALowercaseToken(string value) =>
        Assert.Equal(value, UsageUnit.From(value).Value);

    [Theory]
    [InlineData("")]
    [InlineData("Page")]
    [InlineData("api call")]
    public void From_Throws_ForAValueThatIsNotALowercaseToken(string value) =>
        Assert.ThrowsAny<ArgumentException>(() => UsageUnit.From(value));

    [Fact]
    public void Equality_IsByValue_ForTwoSeparatelyCreatedUnits() =>
        Assert.Equal(UsageUnit.From("page"), UsageUnit.From("page"));

    [Fact]
    public void Value_IsEmpty_ForTheDefaultUnit() =>
        Assert.Equal(string.Empty, default(UsageUnit).Value);

    [Fact]
    public void ToString_IsTheValue_ForAUnit() =>
        Assert.Equal("page", UsageUnit.From("page").ToString());
}
