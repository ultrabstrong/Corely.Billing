namespace Corely.Billing.Quota.Models;

public enum SettleQuotaResultCode
{
    Success,
    UnauthorizedError,
    ValidationError,

    NotRecordedError,
}

public record SettleQuotaResult(
    SettleQuotaResultCode ResultCode,
    string Message,
    decimal SettledQuantity = 0,
    bool Overdrawn = false,
    double? RemainingRatio = null
);
