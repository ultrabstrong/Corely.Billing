namespace Corely.Billing.Quota.Models;

public enum SettleQuotaResultCode
{
    Success,
    UnauthorizedError,

    NotRecordedError,
}

public record SettleQuotaResult(
    SettleQuotaResultCode ResultCode,
    string Message,
    long SettledQuantity = 0,
    bool Overdrawn = false,
    double? RemainingRatio = null
);
