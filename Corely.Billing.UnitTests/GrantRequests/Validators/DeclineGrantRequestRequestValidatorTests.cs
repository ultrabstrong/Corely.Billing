using Corely.Billing.GrantRequests.Models;
using Corely.Billing.GrantRequests.Validators;

namespace Corely.Billing.UnitTests.GrantRequests.Validators;

public class DeclineGrantRequestRequestValidatorTests
{
    private readonly DeclineGrantRequestRequestValidator _validator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Validate_LimitsTheReason_ForEachLength(int? length, bool valid) =>
        Assert.Equal(
            valid,
            _validator
                .Validate(
                    new DeclineGrantRequestRequest(
                        Guid.CreateVersion7(),
                        Guid.CreateVersion7(),
                        length is { } n ? new string('x', n) : null
                    )
                )
                .IsValid
        );
}
