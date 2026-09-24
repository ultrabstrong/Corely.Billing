using Corely.Billing.Grants.Models;

namespace Corely.Billing.Web.Components;

public sealed record GrantBalance(long? Quantity, long Used)
{
    private const double RUNNING_LOW_RATIO = 0.10;

    public static GrantBalance For(Grant grant, long used) => new(grant.Quantity, used);

    public bool IsUnlimited => Quantity is null;

    public bool IsOverdrawn => Quantity is { } quantity && Used > quantity;

    public long Remaining => Quantity is { } quantity ? Math.Max(0, quantity - Used) : 0;

    public long Overdraft => Quantity is { } quantity ? Math.Max(0, Used - quantity) : 0;

    public double UsedRatio =>
        Quantity switch
        {
            null => 0,
            0 => Used > 0 ? 1 : 0,
            { } quantity => Math.Clamp((double)Used / quantity, 0, 1),
        };

    public bool IsRunningLow =>
        !IsUnlimited && !IsOverdrawn && Quantity > 0 && 1 - UsedRatio < RUNNING_LOW_RATIO;
}
