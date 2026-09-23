using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Validators;

namespace Corely.Billing.UnitTests.Grants.Validators;

public class GrantValidatorTests
{
    private readonly GrantValidator _validator = new(TestUsage.Vocabulary);

    private static Grant ValidGrant() =>
        new()
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = 100,
            ValidFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ValidToUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public void Validate_Passes_ForAValidGrant() =>
        Assert.True(_validator.Validate(ValidGrant()).IsValid);

    [Fact]
    public void Validate_Passes_ForAZeroQuantity()
    {
        var grant = ValidGrant();
        grant.Quantity = 0;

        Assert.True(_validator.Validate(grant).IsValid);
    }

    [Fact]
    public void Validate_Passes_ForAnUnlimitedQuantity()
    {
        var grant = ValidGrant();
        grant.Quantity = null;

        Assert.True(_validator.Validate(grant).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAnEmptyAccountId()
    {
        var grant = ValidGrant();
        grant.AccountId = Guid.Empty;

        Assert.False(_validator.Validate(grant).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAnEmptyGrantId()
    {
        var grant = ValidGrant();
        grant.GrantId = Guid.Empty;

        Assert.False(_validator.Validate(grant).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForANegativeQuantity()
    {
        var grant = ValidGrant();
        grant.Quantity = -1;

        Assert.False(_validator.Validate(grant).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAValidityWindowThatEndsBeforeItStarts()
    {
        var grant = ValidGrant();
        grant.ValidToUtc = grant.ValidFromUtc;

        Assert.False(_validator.Validate(grant).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAnUnregisteredOperation()
    {
        var grant = ValidGrant();
        grant.Operation = TestUsage.Unregistered;

        var result = _validator.Validate(grant);

        Assert.False(result.IsValid);
        Assert.Contains("never_registered", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Fails_ForAnUnregisteredUnit()
    {
        var grant = ValidGrant();
        grant.Unit = TestUsage.UnregisteredUnit;

        Assert.False(_validator.Validate(grant).IsValid);
    }
}
