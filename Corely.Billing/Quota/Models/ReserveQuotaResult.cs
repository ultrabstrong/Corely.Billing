namespace Corely.Billing.Quota.Models;

public enum ReserveQuotaResultCode
{
    Success,
    ValidationError,
    UnauthorizedError,

    NoGrantAvailableError,

    InsufficientQuotaError,

    NotRecordedError,
}

public record ReserveQuotaResult(
    ReserveQuotaResultCode ResultCode,
    string Message,
    IReadOnlyList<GrantShare>? Shares = null
);
