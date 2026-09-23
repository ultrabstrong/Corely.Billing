namespace Corely.Billing.Consumption.Models;

public sealed class ReservationOptions
{
    public const string NAME = "ReservationOptions";

    public TimeSpan ReservationTtl { get; set; } = TimeSpan.FromHours(6);
}
