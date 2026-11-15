namespace Corely.Billing.Prices.Models;

public readonly record struct Money(decimal Amount, string Currency)
{
    public const string USD = "USD";
    public const int CURRENCY_LENGTH = 3;
    public const int AMOUNT_PRECISION = 19;
    public const int AMOUNT_DECIMALS = 6;

    public static Money Usd(decimal amount) => new(amount, USD);

    public Money Times(decimal quantity) => this with { Amount = Amount * quantity };

    public override string ToString()
    {
        var amount = Amount.ToString(
            "#,0.00" + new string('#', 26),
            System.Globalization.CultureInfo.InvariantCulture
        );
        return Currency == USD ? $"${amount}" : $"{amount} {Currency}";
    }

    public string? Problem() =>
        Amount < 0 ? "A price cannot be negative."
        : decimal.Round(Amount, AMOUNT_DECIMALS) != Amount
            ? $"A price has at most {AMOUNT_DECIMALS} decimal places."
        : Currency is not { Length: CURRENCY_LENGTH } || !Currency.All(char.IsAsciiLetterUpper)
            ? $"'{Currency}' is not a currency code: three capital letters, such as {USD}."
        : null;
}
