namespace Corely.Billing.Consumption.Models;

internal enum ResolveConsumptionResultCode
{
    Success,

    /// <summary>
    /// The reservations could not be resolved. The hold stays on the grant until the TTL expires,
    /// which is the safe direction to fail: quota is held that should have been freed, rather than
    /// work being delivered that nothing recorded.
    /// </summary>
    NotRecordedError,
}

/// <param name="SettledQuantity">
/// What the resolved rows now total. Zero for a release, and for a settle where no outstanding
/// reservation was found.
/// </param>
internal record ResolveConsumptionResult(
    ResolveConsumptionResultCode ResultCode,
    string Message,
    long SettledQuantity = 0,
    int RowCount = 0
);
