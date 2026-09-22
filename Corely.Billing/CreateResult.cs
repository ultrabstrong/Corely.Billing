using System.Diagnostics.CodeAnalysis;

namespace Corely.Billing;

public enum CreateResultCode
{
    Success = 0,
    ValidationError = 1,
}

public sealed record CreateResult<T>(CreateResultCode ResultCode, string? Message, T? Value)
    where T : class
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => ResultCode == CreateResultCode.Success;

    public static CreateResult<T> Success(T value) => new(CreateResultCode.Success, null, value);

    public static CreateResult<T> Invalid(string message) =>
        new(CreateResultCode.ValidationError, message, null);
}
