using FluentValidation.Results;
using CorelyValidationError = Corely.Billing.Validators.ValidationError;
using CorelyValidationResult = Corely.Billing.Validators.ValidationResult;

namespace Corely.Billing.Validators.Mappers;

internal static class ValidationMapper
{
    extension(ValidationFailure failure)
    {
        public CorelyValidationError ToValidationError()
        {
            return new CorelyValidationError
            {
                Message = failure.ErrorMessage,
                PropertyName = failure.PropertyName,
            };
        }
    }

    extension(FluentValidation.Results.ValidationResult fluentResult)
    {
        public CorelyValidationResult ToValidationResult()
        {
            return new CorelyValidationResult
            {
                Errors = fluentResult.Errors?.Select(e => e.ToValidationError()).ToList(),
            };
        }
    }
}
