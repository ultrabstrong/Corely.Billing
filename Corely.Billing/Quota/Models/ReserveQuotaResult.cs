namespace Corely.Billing.Quota.Models;

public enum ReserveQuotaResultCode
{
    Success,
    ValidationError,
    UnauthorizedError,

    /// <summary>The account has no live grant for this operation and unit at all.</summary>
    NoGrantAvailableError,

    /// <summary>
    /// Grants exist, but not enough room across all of them together. Distinct from having none,
    /// because it is the difference between "buy quota" and "you are out for this cycle".
    /// </summary>
    InsufficientQuotaError,

    /// <summary>The hold could not be written, so the work must not start.</summary>
    NotRecordedError,
}

public record ReserveQuotaResult(
    ReserveQuotaResultCode ResultCode,
    string Message,
    IReadOnlyList<GrantShare>? Shares = null
);
