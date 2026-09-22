using FluentValidation;

namespace Corely.Billing.Validators.FluentValidators;

internal interface IFluentValidatorFactory
{
    IValidator<T> GetValidator<T>();
}
