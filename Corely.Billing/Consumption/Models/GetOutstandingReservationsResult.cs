namespace Corely.Billing.Consumption.Models;

public enum GetOutstandingReservationsResultCode
{
    Success = 0,
    Unauthorized = 1,
    Failed = 2,
}

public record GetOutstandingReservationsResult(
    GetOutstandingReservationsResultCode ResultCode,
    string? Message,
    IReadOnlyList<ConsumptionEvent>? Reservations = null
);
