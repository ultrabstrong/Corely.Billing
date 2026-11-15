namespace Corely.Billing.Prices.Models;

public sealed class ConfiguredPrice
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.USD;

    public Money ToMoney() => new(Amount, Currency);
}
