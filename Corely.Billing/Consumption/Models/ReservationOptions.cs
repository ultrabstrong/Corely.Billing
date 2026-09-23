namespace Corely.Billing.Consumption.Models;

public sealed class ReservationOptions
{
    public const string NAME = "ReservationOptions";

    // Long on purpose: release frees holds; the TTL only catches killed processes and must outlive a next-day replay.
    public TimeSpan ReservationTtl { get; set; } = TimeSpan.FromHours(6);
}
