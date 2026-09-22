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

    /// <summary>Joins this row to the logs of the work that caused it.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Identifies the billable work this row records, the same across every retry of it. A unique
    /// index on <c>(AccountId, IdempotencyKey)</c> is what makes a retry a no-op instead of a second
    /// charge.
    /// </summary>
    public string IdempotencyKey { get; set; } = null!;

    /// <summary>When the reservation resolved; null while it is outstanding.</summary>
    public DateTime? FinalizedUtc { get; set; }

    /// <summary>How it resolved; null while it is outstanding.</summary>
    public ConsumptionOutcome? Outcome { get; set; }

    public Guid? UserId { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
