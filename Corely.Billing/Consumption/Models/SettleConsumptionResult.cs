namespace Corely.Billing.Consumption.Models;

public enum SettleConsumptionResultCode
{
    Success = 0,
    Unauthorized = 1,

    /// <summary>
    /// The reservations could not be resolved. The hold stays on the grant until the TTL expires,
    /// which is the safe direction to fail: quota is held that should have been freed, rather than
    /// work being delivered that nothing recorded.
    /// </summary>
    Failed = 2,
}

/// <param name="SettledQuantity">
/// What the resolved rows now total. Zero for a release, and for a settle where no outstanding
/// reservation was found.
/// </param>
public record SettleConsumptionResult(
    SettleConsumptionResultCode ResultCode,
    string? Message,
    long SettledQuantity = 0,
    int RowCount = 0
);
