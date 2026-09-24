namespace Corely.Billing.Demos.Portal;

internal sealed class SeedClock : TimeProvider
{
    public DateTimeOffset RealNow { get; } = System.GetUtcNow();

    public DateTimeOffset Now { get; set; } = System.GetUtcNow();

    public override DateTimeOffset GetUtcNow() => Now;
}
