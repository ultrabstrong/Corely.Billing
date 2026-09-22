using Microsoft.Extensions.Configuration;

namespace Corely.Billing.UnitTests;

public class BillingOptionsTests
{
    private static BillingOptions Options() => BillingOptions.Create(new ConfigurationManager());

    [Fact]
    public void Create_Throws_ForANullConfiguration() =>
        Assert.Throws<ArgumentNullException>(() => BillingOptions.Create(null!));

    [Fact]
    public void Create_Throws_ForANullEFConfigurationFactory() =>
        Assert.Throws<ArgumentNullException>(() =>
            BillingOptions.Create(new ConfigurationManager(), null!)
        );

    [Fact]
    public void RegisterOperation_KeepsRegistrationOrder_ForSeveralOperations()
    {
        var options = Options()
            .RegisterOperation("second", "Second")
            .RegisterOperation("first", "First");

        Assert.Equal(["second", "first"], options.Operations.Keys.Select(o => o.Value));
    }

    [Fact]
    public void RegisterOperation_KeepsTheLastDisplayName_ForADuplicateOperation()
    {
        var options = Options().RegisterOperation("a", "A").RegisterOperation("a", "A again");

        Assert.Equal("A again", Assert.Single(options.Operations).Value);
    }

    [Fact]
    public void RegisterUnit_KeepsTheLastDisplayName_ForADuplicateUnit()
    {
        var options = Options().RegisterUnit("page", "page").RegisterUnit("page", "pages");

        Assert.Equal("pages", Assert.Single(options.Units).Value);
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("has space")]
    [InlineData("")]
    public void RegisterOperation_Throws_ForAnInvalidToken(string value) =>
        Assert.ThrowsAny<ArgumentException>(() => Options().RegisterOperation(value, "Name"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RegisterUnit_Throws_ForABlankDisplayName(string displayName) =>
        Assert.ThrowsAny<ArgumentException>(() => Options().RegisterUnit("page", displayName));

    [Fact]
    public void UseTelemetry_Throws_ForANullFactory() =>
        Assert.Throws<ArgumentNullException>(() => Options().UseTelemetry(null!));

    [Fact]
    public void DecorateServices_Throws_ForANullAction() =>
        Assert.Throws<ArgumentNullException>(() => Options().DecorateServices(null!));
}
