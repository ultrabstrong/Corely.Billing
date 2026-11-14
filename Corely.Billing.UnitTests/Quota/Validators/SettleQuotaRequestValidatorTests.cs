using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Validators;

namespace Corely.Billing.UnitTests.Quota.Validators;

public class SettleQuotaRequestValidatorTests
{
    private readonly SettleQuotaRequestValidator _validator = new(TestUsage.Vocabulary);

    private static SettleQuotaRequest Credits(decimal actual) =>
        new(Guid.CreateVersion7(), TestUsage.Spend, TestUsage.Credit, actual);

    [Theory]
    [InlineData("0")]
    [InlineData("0.000001")]
    [InlineData("345.5")]
    public void Validate_Passes_ForAQuantityTheUnitHolds(string actual) =>
        Assert.True(_validator.Validate(Credits(decimal.Parse(actual))).IsValid);

    [Fact]
    public void Validate_Fails_ForAQuantityFinerThanTheUnit() =>
        Assert.False(_validator.Validate(Credits(0.0000001m)).IsValid);

    [Fact]
    public void Validate_Fails_ForAFractionOfAWholeUnit() =>
        Assert.False(
            _validator
                .Validate(
                    new SettleQuotaRequest(
                        Guid.CreateVersion7(),
                        TestUsage.Extraction,
                        TestUsage.Page,
                        1.5m
                    )
                )
                .IsValid
        );

    [Fact]
    public void Validate_Fails_ForANegativeQuantity() =>
        Assert.False(_validator.Validate(Credits(-1)).IsValid);

    [Fact]
    public void Validate_Fails_ForAnEmptyAccountId() =>
        Assert.False(_validator.Validate(Credits(1) with { AccountId = Guid.Empty }).IsValid);
}
