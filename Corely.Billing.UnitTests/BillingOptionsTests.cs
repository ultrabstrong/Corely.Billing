using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
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
            .RegisterOperation("second", "Second", GrantKind.Feature)
            .RegisterOperation("first", "First", GrantKind.Feature);

        Assert.Equal(["second", "first"], options.Operations.Keys.Select(o => o.Value));
    }

    [Fact]
    public void RegisterOperation_KeepsTheLastDisplayName_ForADuplicateOperation()
    {
        var options = Options()
            .RegisterOperation("a", "A", GrantKind.Feature)
            .RegisterOperation("a", "A again", GrantKind.Feature);

        Assert.Equal("A again", Assert.Single(options.Operations).Value.DisplayName);
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
        Assert.ThrowsAny<ArgumentException>(() =>
            Options().RegisterOperation(value, "Name", GrantKind.Feature)
        );

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

    [Fact]
    public void DeclarationProblem_IsNull_ForOneOperationOfEachKindDeclaredProperly() =>
        Assert.Null(
            Options()
                .RegisterUnit("page", "page")
                .RegisterUnit("day", "day")
                .RegisterUnit("user", "user")
                .RegisterOperation("extract", "Extract", GrantKind.Consumable, "page")
                .RegisterOperation("members", "Members", GrantKind.Capacity, "user")
                .RegisterOperation("retain", "Retain", GrantKind.Allowance, "day")
                .RegisterOperation("sftp", "SFTP", GrantKind.Feature)
                .DeclarationProblem()
        );

    [Fact]
    public void DeclarationProblem_ReportsIt_ForNoOperations() =>
        Assert.NotNull(Options().DeclarationProblem());

    [Theory]
    [InlineData(GrantKind.Consumable)]
    [InlineData(GrantKind.Capacity)]
    [InlineData(GrantKind.Allowance)]
    public void DeclarationProblem_ReportsIt_ForACountedKindWithNoUnit(GrantKind kind) =>
        Assert.Contains(
            "names no unit",
            Options().RegisterOperation("x", "X", kind).DeclarationProblem()
        );

    [Fact]
    public void DeclarationProblem_ReportsIt_ForAFeatureWithAUnit() =>
        Assert.Contains(
            "is a feature and names a unit",
            Options()
                .RegisterUnit("day", "day")
                .RegisterOperation("sftp", "SFTP", GrantKind.Feature, "day")
                .DeclarationProblem()
        );

    [Fact]
    public void DeclarationProblem_ReportsIt_ForAnUnregisteredUnit() =>
        Assert.Contains(
            "not registered",
            Options().RegisterOperation("x", "X", GrantKind.Consumable, "page").DeclarationProblem()
        );
}
