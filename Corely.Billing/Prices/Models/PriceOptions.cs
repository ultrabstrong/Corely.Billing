namespace Corely.Billing.Prices.Models;

public sealed class PriceOptions
{
    public const string NAME = "PriceOptions";

    public Dictionary<string, ConfiguredPrice> Prices { get; set; } = [];
}
