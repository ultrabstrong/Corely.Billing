using Corely.Billing.Consumption.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;
using FluentValidation;

namespace Corely.Billing.Quota.Validators;

internal class SettleQuotaRequestValidator : AbstractValidator<SettleQuotaRequest>
{
    public SettleQuotaRequestValidator(IUsageVocabulary vocabulary)
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.ActualQuantity).GreaterThanOrEqualTo(ConsumptionEvent.QUANTITY_MIN_VALUE);
        RuleFor(x => x.ActualQuantity)
            .Must((request, quantity) => vocabulary.Allows(request.Unit, quantity))
            .WithMessage(x =>
                $"Unit '{x.Unit}' does not allow a quantity as precise as {x.ActualQuantity}."
            );
    }
}
