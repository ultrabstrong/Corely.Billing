namespace Corely.Billing.Consumption.Models;

internal enum ReserveConsumptionResultCode
{
    Success,
    ValidationError,

    NotRecordedError,
}

internal record ReserveConsumptionResult(ReserveConsumptionResultCode ResultCode, string Message);
