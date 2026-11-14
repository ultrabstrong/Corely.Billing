using Corely.Billing.Usage;

namespace Corely.Billing.UnitTests.Usage;

public class UsageUnitDefinitionTests
{
    [Theory]
    [InlineData(0, "3", true)]
    [InlineData(0, "3.000000", true)]
    [InlineData(0, "3.5", false)]
    [InlineData(6, "0.000001", true)]
    [InlineData(6, "4.500000", true)]
    [InlineData(6, "0.0000001", false)]
    public void Allows_AcceptsOnlyTheRegisteredPlaces_ForAQuantity(
        int decimals,
        string quantity,
        bool allowed
    ) =>
        Assert.Equal(
            allowed,
            new UsageUnitDefinition(UsageUnit.From("unit"), "unit", decimals).Allows(
                decimal.Parse(quantity)
            )
        );
}
