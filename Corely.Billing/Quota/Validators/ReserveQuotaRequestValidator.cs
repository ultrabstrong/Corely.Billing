using Corely.Billing.Consumption.Constants;
using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;
using FluentValidation;

namespace Corely.Billing.Quota.Validators;

internal class ReserveQuotaRequestValidator : AbstractValidator<ReserveQuotaRequest>
{
    public ReserveQuotaRequestValidator(IUsageVocabulary vocabulary)
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(ConsumptionConstants.QUANTITY_MIN_VALUE);
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(ConsumptionConstants.PROVIDER_MAX_LENGTH);
        RuleFor(x => x.Operation)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Operation '{x.Operation}' is not a registered usage operation.");
        RuleFor(x => x.Unit)
            .Must(vocabulary.Knows)
            .WithMessage(x => $"Unit '{x.Unit}' is not a registered usage unit.");
    }
}
