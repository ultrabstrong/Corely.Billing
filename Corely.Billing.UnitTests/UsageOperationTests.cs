using Corely.Billing;

namespace Corely.Billing.UnitTests;

public class UsageOperationTests
{
    [Theory]
    [InlineData("document_extraction")]
    [InlineData("noop")]
    [InlineData("step_2")]
    public void From_KeepsTheValue_ForALowercaseToken(string value) =>
        Assert.Equal(value, UsageOperation.From(value).Value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DocumentExtraction")]
    [InlineData("document extraction")]
    [InlineData("document-extraction")]
    [InlineData("document.extraction")]
    public void From_Throws_ForAValueThatIsNotALowercaseToken(string value) =>
        Assert.ThrowsAny<ArgumentException>(() => UsageOperation.From(value));

    [Fact]
    public void From_Throws_ForAValueLongerThanTheMaximum() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            UsageOperation.From(new string('a', UsageToken.MAX_LENGTH + 1))
        );

    [Fact]
    public void Equality_IsByValue_ForTwoSeparatelyCreatedOperations() =>
        Assert.Equal(UsageOperation.From("page_count"), UsageOperation.From("page_count"));

    [Fact]
    public void Value_IsEmpty_ForTheDefaultOperation() =>
        Assert.Equal(string.Empty, default(UsageOperation).Value);

    [Fact]
    public void CompareTo_OrdersOrdinally_ForTwoOperations() =>
        Assert.True(UsageOperation.From("a").CompareTo(UsageOperation.From("b")) < 0);

    [Fact]
    public void ToString_IsTheValue_ForAnOperation() =>
        Assert.Equal("document_extraction", UsageOperation.From("document_extraction").ToString());
}
