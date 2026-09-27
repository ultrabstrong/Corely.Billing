using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class StringExtensionsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData(" padded", "\" padded\"")]
    public void CsvCell_QuotesPerRfc4180_ForText(string? value, string expected) =>
        Assert.Equal(expected, value.CsvCell());

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@x", "'@x")]
    [InlineData("\tx", "'\tx")]
    public void CsvCell_DisarmsFormulas_ForTextASpreadsheetWouldRun(
        string value,
        string expected
    ) => Assert.Equal(expected, value.CsvCell());
}
