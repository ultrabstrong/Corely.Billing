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

    [Fact]
    public void TryFrom_GivesTheValue_ForALowercaseToken()
    {
        Assert.True(UsageUnit.TryFrom("page", out var unit));
        Assert.Equal("page", unit.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not_Lowercase")]
    [InlineData("has space")]
    public void TryFrom_IsFalse_ForAValueThatIsNotALowercaseToken(string? value)
    {
        Assert.False(UsageUnit.TryFrom(value, out var unit));
        Assert.Equal(default, unit);
    }

    [Fact]
    public void TryFrom_IsFalse_ForAValuePastTheMaximumLength() =>
        Assert.False(UsageUnit.TryFrom(new string('a', UsageUnit.MAX_LENGTH + 1), out _));
}
