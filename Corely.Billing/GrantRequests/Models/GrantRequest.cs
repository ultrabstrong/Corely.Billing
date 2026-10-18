using Corely.Billing.Usage;

namespace Corely.Billing.GrantRequests.Models;

public class GrantRequest
{
    public Guid GrantRequestId { get; set; }
    public Guid AccountId { get; set; }
    public UsageOperation Operation { get; set; }
    public UsageUnit Unit { get; set; }
    public long Quantity { get; set; }
    public string? Note { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime RequestedUtc { get; set; }
    public GrantRequestStatus Status { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public Guid? GrantId { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
