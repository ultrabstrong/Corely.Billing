using Corely.Billing.Grants.Constants;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using FluentValidation;

namespace Corely.Billing.Grants.Validators;

internal class GrantValidator : AbstractValidator<Grant>
{
    public GrantValidator(IUsageVocabulary vocabulary)
    {
        RuleFor(x => x.GrantId).NotEmpty();
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(GrantConstants.QUANTITY_MIN_VALUE);
        RuleFor(x => x.ValidToUtc)
            .GreaterThan(x => x.ValidFromUtc)
            .WithMessage("Valid To must be after Valid From.");
        RuleFor(x => x.Operation)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Operation '{x.Operation}' is not a registered usage operation.");
        RuleFor(x => x.Unit)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Unit '{x.Unit}' is not a registered usage unit.");
    }
}
