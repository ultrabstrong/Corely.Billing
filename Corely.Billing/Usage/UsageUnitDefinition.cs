namespace Corely.Billing.Usage;

public sealed record UsageUnitDefinition(UsageUnit Unit, string DisplayName, int Decimals = 0)
{
    public const int DECIMALS_MAX_VALUE = 6;
    internal const int QUANTITY_PRECISION = 19;

    public bool Allows(decimal quantity) => decimal.Round(quantity, Decimals) == quantity;
}
