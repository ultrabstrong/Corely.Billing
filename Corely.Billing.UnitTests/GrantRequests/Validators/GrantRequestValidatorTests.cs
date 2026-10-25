using Corely.Billing.GrantRequests.Models;
using Corely.Billing.GrantRequests.Validators;

namespace Corely.Billing.UnitTests.GrantRequests.Validators;

public class GrantRequestValidatorTests
{
    private readonly GrantRequestValidator _validator = new(TestUsage.Vocabulary);

    private static GrantRequest Valid(Action<GrantRequest>? change = null)
    {
        var request = new GrantRequest
        {
            GrantRequestId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = 1,
        };
        change?.Invoke(request);
        return request;
    }

    [Fact]
    public void Validate_Passes_ForAValidRequest() =>
        Assert.True(_validator.Validate(Valid()).IsValid);

    [Fact]
    public void Validate_Fails_ForZero() =>
        Assert.False(_validator.Validate(Valid(r => r.Quantity = 0)).IsValid);

    [Fact]
    public void Validate_Fails_ForANoteTooLong() =>
        Assert.False(_validator.Validate(Valid(r => r.Note = new string('x', 501))).IsValid);

    [Fact]
    public void Validate_Fails_ForAnUnregisteredOperation() =>
        Assert.False(_validator.Validate(Valid(r => r.Operation = TestUsage.Unregistered)).IsValid);

    [Fact]
    public void Validate_Fails_ForAFeatureWithAQuantity() =>
        Assert.False(
            _validator
                .Validate(Valid(r => r.Kind = Corely.Billing.Grants.Models.GrantKind.Feature))
                .IsValid
        );

    [Fact]
    public void Validate_Passes_ForAFeatureWithNoQuantity() =>
        Assert.True(
            _validator
                .Validate(
                    Valid(r =>
                    {
                        r.Kind = Corely.Billing.Grants.Models.GrantKind.Feature;
                        r.Quantity = null;
                    })
                )
                .IsValid
        );

    [Fact]
    public void Validate_Fails_ForAnEmptyAccount() =>
        Assert.False(_validator.Validate(Valid(r => r.AccountId = Guid.Empty)).IsValid);
}
