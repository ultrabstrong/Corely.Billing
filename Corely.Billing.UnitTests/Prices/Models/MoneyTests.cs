using Corely.Billing.Prices.Models;

namespace Corely.Billing.UnitTests.Prices.Models;

public class MoneyTests
{
    [Fact]
    public void Times_KeepsEveryDigit_ForAFractionalProduct() =>
        Assert.Equal(Money.Usd(0.0045m), Money.Usd(0.001m).Times(4.5m));

    [Fact]
    public void Problem_IsNull_ForAWholeDollarPrice() => Assert.Null(Money.Usd(12).Problem());

    [Theory]
    [InlineData(-1, "USD", "negative")]
    [InlineData(0.0000001, "USD", "decimal places")]
    [InlineData(1, "usd", "currency code")]
    [InlineData(1, "US", "currency code")]
    public void Problem_NamesIt_ForAnInvalidPrice(decimal amount, string currency, string named) =>
        Assert.Contains(named, new Money(amount, currency).Problem());

    [Theory]
    [InlineData(2000, "$2,000.00")]
    [InlineData(0.1, "$0.10")]
    [InlineData(0.0045, "$0.0045")]
    [InlineData(0.000000001, "$0.000000001")]
    public void ToString_ShowsEveryDigitAndAtLeastCents_ForDollars(
        decimal amount,
        string expected
    ) => Assert.Equal(expected, Money.Usd(amount).ToString());

    [Fact]
    public void ToString_NamesTheCurrency_ForAnotherCurrency() =>
        Assert.Equal("1.50 EUR", new Money(1.5m, "EUR").ToString());
}
