namespace Corely.Billing.Consumption.Models;

internal enum ReserveConsumptionResultCode
{
    Success,
    ValidationError,

    /// <summary>
    /// Nothing was recorded. The work must not start, since whatever it cost would reach no
    /// ledger.
    /// </summary>
    NotRecordedError,
}

internal record ReserveConsumptionResult(ReserveConsumptionResultCode ResultCode, string Message);
