using Corely.Billing.Consumption.Constants;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Validators;

namespace Corely.Billing.UnitTests.Consumption.Validators;

public class ConsumptionEventValidatorTests
{
    private readonly ConsumptionEventValidator _validator = new(TestUsage.Vocabulary);

    private static ConsumptionEvent ValidEvent() =>
        new()
        {
            AccountId = Guid.CreateVersion7(),
            GrantId = Guid.CreateVersion7(),
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = ConsumptionConstants.QUANTITY_MIN_VALUE,
            Provider = "prov",
            UtcTimestamp = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public void Validate_Passes_ForAValidEvent() =>
        Assert.True(_validator.Validate(ValidEvent()).IsValid);

    [Fact]
    public void Validate_Passes_ForAnEventWithAUser()
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.UserId = Guid.CreateVersion7();

        Assert.True(_validator.Validate(consumptionEvent).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAnEmptyAccountId()
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.AccountId = Guid.Empty;

        Assert.False(_validator.Validate(consumptionEvent).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAnEmptyGrantId()
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.GrantId = Guid.Empty;

        Assert.False(_validator.Validate(consumptionEvent).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAQuantityBelowTheMinimum()
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.Quantity = ConsumptionConstants.QUANTITY_MIN_VALUE - 1;

        Assert.False(_validator.Validate(consumptionEvent).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_Fails_ForAnEmptyProvider(string? provider)
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.Provider = provider!;

        Assert.False(_validator.Validate(consumptionEvent).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAProviderTooLong()
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.Provider = new string('p', ConsumptionConstants.PROVIDER_MAX_LENGTH + 1);

        Assert.False(_validator.Validate(consumptionEvent).IsValid);
    }

    [Fact]
    public void Validate_Fails_ForAnUnregisteredOperation()
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.Operation = TestUsage.Unregistered;

        var result = _validator.Validate(consumptionEvent);

        Assert.False(result.IsValid);
        Assert.Contains("never_registered", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Fails_ForAnUnregisteredUnit()
    {
        var consumptionEvent = ValidEvent();
        consumptionEvent.Unit = TestUsage.UnregisteredUnit;

        Assert.False(_validator.Validate(consumptionEvent).IsValid);
    }
}
