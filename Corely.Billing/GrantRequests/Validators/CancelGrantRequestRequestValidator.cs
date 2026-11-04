using Corely.Billing.GrantRequests.Models;
using FluentValidation;

namespace Corely.Billing.GrantRequests.Validators;

internal class CancelGrantRequestRequestValidator : AbstractValidator<CancelGrantRequestRequest>
{
    public CancelGrantRequestRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(GrantRequest.DECISION_REASON_MAX_LENGTH);
    }
}
