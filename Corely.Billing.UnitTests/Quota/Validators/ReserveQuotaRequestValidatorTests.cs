using Corely.Billing.Consumption.Constants;
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
                        Provider = new string('p', ConsumptionConstants.PROVIDER_MAX_LENGTH + 1),
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
