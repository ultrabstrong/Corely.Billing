namespace Corely.Billing.Consumption.Models;

public enum SaveConsumptionResultCode
{
    Success = 0,
    Unauthorized = 1,

    /// <summary>
    /// The provider was called and billed us, and nothing was recorded. Revenue is being lost while
    /// this code is returned, so callers must not treat it as a warning.
    /// </summary>
    Failed = 2,
    UnknownUsage = 3,
}

public record SaveConsumptionResult(SaveConsumptionResultCode ResultCode, string? Message);
