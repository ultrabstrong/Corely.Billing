namespace Corely.Billing.Quota.Models;

public enum ReserveQuotaResultCode
{
    Success = 0,
    Unauthorized = 1,

    /// <summary>The account has no live grant for this operation and unit at all.</summary>
    NoGrantAvailable = 2,

    /// <summary>
    /// Grants exist, but not enough room across all of them together. Distinct from having none,
    /// because it is the difference between "buy quota" and "you are out for this cycle".
    /// </summary>
    InsufficientQuota = 3,

    /// <summary>The hold could not be written, so the work must not start.</summary>
    NotRecorded = 4,
}

public sealed record ReserveQuotaResult(
    ReserveQuotaResultCode ResultCode,
    string? Message,
    IReadOnlyList<GrantShare>? Shares = null
);

public enum SettleQuotaResultCode
{
    Success = 0,
    Unauthorized = 1,
    Failed = 2,
}

/// <param name="Overdrawn">
/// The work cost more than every remaining grant could cover. The results are still delivered -- the
/// provider has already been paid, so refusing afterwards means eating the cost, giving the customer
/// nothing, and nothing stopping it recurring -- and the overdraft lands on the last grant.
/// </param>
public sealed record SettleQuotaResult(
    SettleQuotaResultCode ResultCode,
    string? Message,
    long SettledQuantity = 0,
    bool Overdrawn = false,
    double? RemainingRatio = null
);
