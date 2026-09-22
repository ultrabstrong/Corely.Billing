namespace Corely.Billing.Consumption.Models;

public sealed class ReservationOptions
{
    public const string NAME = "ReservationOptions";

    /// <summary>
    /// How long an unresolved reservation keeps holding quota.
    /// </summary>
    /// <remarks>
    /// Generous on purpose. Release is the primary mechanism -- a step that fails terminally gives
    /// its hold back immediately -- so this only has to catch a process killed mid-flight. Tuned
    /// short enough to free quota promptly it would instead expire holds that a manual replay the
    /// next morning still needs, and no single value can be both.
    /// </remarks>
    public TimeSpan ReservationTtl { get; set; } = TimeSpan.FromHours(6);
}
