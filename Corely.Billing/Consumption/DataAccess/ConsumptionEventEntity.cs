using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Corely.DataAccess.Interfaces.Entities;

namespace Corely.Billing.Consumption.DataAccess;

public class ConsumptionEventEntity : IHasCreatedUtc
{
    public Guid ConsumptionId { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Guid AccountId { get; set; }
    public long Quantity { get; set; }
    public UsageUnit Unit { get; set; }
    public UsageOperation Operation { get; set; }
    public string Provider { get; set; } = default!;
    public DateTime UtcTimestamp { get; set; }
    public Guid CorrelationId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public Guid GrantId { get; set; }

    /// <summary>When the reservation resolved. Null while it is still outstanding.</summary>
    public DateTime? FinalizedUtc { get; set; }

    /// <summary>How it resolved. Null while it is still outstanding.</summary>
    public ConsumptionOutcome? Outcome { get; set; }

    public Guid? UserId { get; set; }
    public string? TagsJson { get; set; }
}
