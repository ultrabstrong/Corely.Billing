namespace Corely.Billing.Quota.Models;

public enum SettleQuotaResultCode
{
    Success,
    UnauthorizedError,

    /// <summary>
    /// The ledger could not be updated. The hold stays until its TTL expires: quota held that should
    /// have been freed, rather than work delivered that nothing recorded.
    /// </summary>
    NotRecordedError,
}

/// <param name="Overdrawn">
/// The work cost more than every remaining grant could cover. The results are still delivered -- the
/// work has already been paid for, so refusing afterwards means eating the cost, giving the customer
/// nothing, and nothing stopping it recurring -- and the overdraft lands on the last grant.
/// </param>
/// <param name="RemainingRatio">
/// What is left across the account's live grants after this charge, from 0 to 1.
/// </param>
public record SettleQuotaResult(
    SettleQuotaResultCode ResultCode,
    string Message,
    long SettledQuantity = 0,
    bool Overdrawn = false,
    double? RemainingRatio = null
);
