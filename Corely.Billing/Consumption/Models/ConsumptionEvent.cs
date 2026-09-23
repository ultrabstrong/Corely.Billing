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
}
