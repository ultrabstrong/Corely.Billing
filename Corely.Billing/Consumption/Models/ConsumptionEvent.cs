using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Models;

public class ConsumptionEvent
{
    public Guid ConsumptionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid GrantId { get; set; }
    public UsageOperation Operation { get; set; }
    public UsageUnit Unit { get; set; }
    public long Quantity { get; set; }
    public string Provider { get; set; } = null!;
    public DateTime UtcTimestamp { get; set; }

    public Guid CorrelationId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public DateTime? FinalizedUtc { get; set; }

    public ConsumptionOutcome? Outcome { get; set; }

    public Guid? UserId { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    // Settled rows, and holds younger than the reservation TTL, are what a balance subtracts.
    public bool CountsTowardBalance(DateTime liveFromUtc) =>
        Outcome == ConsumptionOutcome.Settled
        || (FinalizedUtc is null && UtcTimestamp >= liveFromUtc);

    // The operation scope the row was charged under. Every row's key is the scope followed by
    // "|{operation}|{unit}|{grantId:N}", all known here, so the scope is whatever precedes that.
    // One charge split across grants is several rows sharing one scope.
    public string? IdempotencyScope
    {
        get
        {
            var suffix = $"|{Operation}|{Unit}|{GrantId:N}";
            return IdempotencyKey?.EndsWith(suffix, StringComparison.Ordinal) == true
                ? IdempotencyKey[..^suffix.Length]
                : null;
        }
    }
}
