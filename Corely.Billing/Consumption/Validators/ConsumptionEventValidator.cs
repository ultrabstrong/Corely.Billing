using Corely.Billing.Consumption.Models;
using Corely.Billing.Usage;
using FluentValidation;

namespace Corely.Billing.Consumption.Validators;

internal class ConsumptionEventValidator : AbstractValidator<ConsumptionEvent>
{
    public ConsumptionEventValidator(IUsageVocabulary vocabulary)
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.GrantId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(ConsumptionEvent.QUANTITY_MIN_VALUE);
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(ConsumptionEvent.PROVIDER_MAX_LENGTH);
        RuleFor(x => x.Operation)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Operation '{x.Operation}' is not a registered usage operation.");
        RuleFor(x => x.Unit)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Unit '{x.Unit}' is not a registered usage unit.");
        RuleFor(x => x.Quantity)
            .Must((consumption, quantity) => vocabulary.Allows(consumption.Unit, quantity))
            .WithMessage(x =>
                $"Unit '{x.Unit}' does not allow a quantity as precise as {x.Quantity}."
            );
    }
}
