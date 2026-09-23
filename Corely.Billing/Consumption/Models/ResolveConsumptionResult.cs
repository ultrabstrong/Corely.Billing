namespace Corely.Billing.Consumption.Models;

internal enum ResolveConsumptionResultCode
{
    Success,

    NotRecordedError,
}

internal record ResolveConsumptionResult(
    ResolveConsumptionResultCode ResultCode,
    string Message,
    long SettledQuantity = 0,
    int RowCount = 0
);
