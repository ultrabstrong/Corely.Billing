namespace Corely.Billing.Consumption.Models;

internal enum ResolveConsumptionResultCode
{
    Success,

    NotRecordedError,
}

internal record ResolveConsumptionResult(
    ResolveConsumptionResultCode ResultCode,
    string Message,
    decimal SettledQuantity = 0,
    int RowCount = 0
);
