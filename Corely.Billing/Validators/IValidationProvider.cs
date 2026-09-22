namespace Corely.Billing.Validators;

internal interface IValidationProvider
{
    public ValidationResult Validate<T>(T model);

    public ValidationResult ValidateAndLog<T>(T model);
}
