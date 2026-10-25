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
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(GrantConstants.QUANTITY_MIN_VALUE);
        RuleFor(x => x.ValidToUtc)
            .GreaterThan(x => x.ValidFromUtc)
            .WithMessage("Valid To must be after Valid From.");
        RuleFor(x => x.Operation)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Operation '{x.Operation}' is not a registered usage operation.");
        RuleFor(x => x.Unit)
            .Must(unit => unit is not { } known || vocabulary.Knows(known))
            .WithMessage(x => $"Unit '{x.Unit}' is not a registered usage unit.");
        RuleFor(x => x.Unit)
            .NotNull()
            .When(x => x.Kind != GrantKind.Feature)
            .WithMessage(x => $"A {x.Kind} grant is counted in a unit.");
        When(
            x => x.Kind == GrantKind.Feature,
            () =>
            {
                RuleFor(x => x.Quantity).Null().WithMessage("A feature grant has no quantity.");
                RuleFor(x => x.Unit).Null().WithMessage("A feature grant has no unit.");
            }
        );
    }
}
