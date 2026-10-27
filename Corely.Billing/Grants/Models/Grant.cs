using Corely.Billing.Grants.Enums;
using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Models;

public class Grant
{
    internal const int QUANTITY_MIN_VALUE = 0;
    internal const int KIND_MAX_LENGTH = 20;
    public static readonly DateTime NEVER_EXPIRES_UTC = new(
        9999,
        12,
        31,
        0,
        0,
        0,
        DateTimeKind.Utc
    );

    public Guid GrantId { get; set; }
    public Guid AccountId { get; set; }
    public UsageOperation Operation { get; set; }
    public GrantKind Kind { get; set; }
    public UsageUnit? Unit { get; set; }
    public long? Quantity { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool Disabled { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    public long? Used { get; set; }
    public long? Remaining { get; set; }
    public long? InUse { get; set; }

    public GrantType GetGrantType() =>
        (Kind, Quantity) switch
        {
            (GrantKind.Consumable, null) => GrantType.Unlimited,
            (GrantKind.Consumable, _) => GrantType.Balance,
            (GrantKind.Capacity, null) => GrantType.UnlimitedCapacity,
            (GrantKind.Capacity, _) => GrantType.Capacity,
            (GrantKind.Allowance, null) => GrantType.UnlimitedAllowance,
            (GrantKind.Allowance, _) => GrantType.Allowance,
            (GrantKind.Feature, _) => GrantType.Feature,
            _ => throw new InvalidOperationException($"Grant kind {Kind} has no grant type."),
        };

    public bool NeverExpires() => ValidToUtc >= Grant.NEVER_EXPIRES_UTC;

    public void WithUse(long used)
    {
        Used = used;
        Remaining = Quantity is { } quantity ? Math.Max(0, quantity - used) : null;
    }

    public bool IsActiveAt(DateTime utc) => !Disabled && ValidFromUtc <= utc && utc <= ValidToUtc;
}
