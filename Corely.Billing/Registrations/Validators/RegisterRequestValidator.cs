using Corely.Billing.Registrations.Constants;
using Corely.Billing.Registrations.Models;
using FluentValidation;

namespace Corely.Billing.Registrations.Validators;

internal class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Reference).MaximumLength(RegistrationConstants.REFERENCE_MAX_LENGTH);
        RuleFor(x => x.Description).MaximumLength(RegistrationConstants.DESCRIPTION_MAX_LENGTH);
    }
}
