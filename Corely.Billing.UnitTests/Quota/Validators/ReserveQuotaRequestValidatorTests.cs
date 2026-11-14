using Corely.Billing.Consumption.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Validators;

namespace Corely.Billing.UnitTests.Quota.Validators;

public class ReserveQuotaRequestValidatorTests
{
    private readonly ReserveQuotaRequestValidator _validator = new(TestUsage.Vocabulary);

    private static ReserveQuotaRequest ValidRequest() =>
        new(Guid.CreateVersion7(), TestUsage.Extraction, TestUsage.Page, 1, "prov");

    [Fact]
    public void Validate_Passes_ForAValidRequest() =>
        Assert.True(_validator.Validate(ValidRequest()).IsValid);

    [Fact]
    public void Validate_Passes_ForAZeroQuantity() =>
        Assert.True(_validator.Validate(ValidRequest() with { Quantity = 0 }).IsValid);

    [Fact]
    public void Validate_Fails_ForAFractionOfAWholeUnit() =>
        Assert.False(_validator.Validate(ValidRequest() with { Quantity = 1.5m }).IsValid);

    [Theory]
    [InlineData("0.000001", true)]
    [InlineData("3.333333", true)]
    [InlineData("0.0000001", false)]
    public void Validate_AllowsTheUnitsDecimals_ForACreditQuantity(string quantity, bool valid) =>
        Assert.Equal(
            valid,
            _validator
                .Validate(
                    ValidRequest() with
                    {
                        Operation = TestUsage.Spend,
                        Unit = TestUsage.Credit,
                        Quantity = decimal.Parse(quantity),
                    }
                )
                .IsValid
        );

    [Fact]
    public void Validate_Fails_ForAnEmptyAccountId() =>
        Assert.False(_validator.Validate(ValidRequest() with { AccountId = Guid.Empty }).IsValid);

    [Fact]
    public void Validate_Fails_ForANegativeQuantity() =>
        Assert.False(_validator.Validate(ValidRequest() with { Quantity = -1 }).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_Fails_ForAnEmptyProvider(string provider) =>
        Assert.False(_validator.Validate(ValidRequest() with { Provider = provider }).IsValid);

    [Fact]
    public void Validate_Fails_ForAProviderTooLong() =>
        Assert.False(
            _validator
                .Validate(
                    ValidRequest() with
                    {
                        Provider = new string('p', ConsumptionEvent.PROVIDER_MAX_LENGTH + 1),
                    }
                )
                .IsValid
        );

    [Fact]
    public void Validate_Fails_ForAnUnregisteredOperation() =>
        Assert.False(
            _validator.Validate(ValidRequest() with { Operation = TestUsage.Unregistered }).IsValid
        );

    [Fact]
    public void Validate_Fails_ForAnUnregisteredUnit() =>
        Assert.False(
            _validator.Validate(ValidRequest() with { Unit = TestUsage.UnregisteredUnit }).IsValid
        );
}
