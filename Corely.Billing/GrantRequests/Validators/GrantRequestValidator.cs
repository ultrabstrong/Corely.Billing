using Corely.Billing.GrantRequests.Constants;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Usage;
using FluentValidation;

namespace Corely.Billing.GrantRequests.Validators;

internal class GrantRequestValidator : AbstractValidator<GrantRequest>
{
    public GrantRequestValidator(IUsageVocabulary vocabulary)
    {
        RuleFor(x => x.GrantRequestId).NotEmpty();
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(GrantRequestConstants.QUANTITY_MIN_VALUE);
        RuleFor(x => x.Note).MaximumLength(GrantRequestConstants.NOTE_MAX_LENGTH);
        RuleFor(x => x.Operation)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Operation '{x.Operation}' is not a registered usage operation.");
        RuleFor(x => x.Unit)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Unit '{x.Unit}' is not a registered usage unit.");
    }
}
