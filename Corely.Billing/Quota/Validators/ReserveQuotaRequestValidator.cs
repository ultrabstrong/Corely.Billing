using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;
using FluentValidation;

namespace Corely.Billing.Quota.Validators;

internal class ReserveQuotaRequestValidator : AbstractValidator<ReserveQuotaRequest>
{
    public ReserveQuotaRequestValidator(IUsageVocabulary vocabulary)
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(ConsumptionEvent.QUANTITY_MIN_VALUE);
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(ConsumptionEvent.PROVIDER_MAX_LENGTH);
        RuleFor(x => x.Operation)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Operation '{x.Operation}' is not a registered usage operation.");
        RuleFor(x => x)
            .Must(x =>
                vocabulary.Definition(x.Operation)?.Declares(GrantKind.Consumable, x.Unit) != false
            )
            .WithMessage(x =>
                $"Operation '{x.Operation}' is not declared as consumable in '{x.Unit}'."
            );
    }
}
