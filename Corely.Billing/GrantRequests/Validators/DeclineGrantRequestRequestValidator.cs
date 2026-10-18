using Corely.Billing.GrantRequests.Constants;
using Corely.Billing.GrantRequests.Models;
using FluentValidation;

namespace Corely.Billing.GrantRequests.Validators;

internal class DeclineGrantRequestRequestValidator : AbstractValidator<DeclineGrantRequestRequest>
{
    public DeclineGrantRequestRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(GrantRequestConstants.DECISION_REASON_MAX_LENGTH);
    }
}
