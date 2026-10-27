using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.GrantRequests.Models;

public class GrantRequest
{
    internal const int QUANTITY_MIN_VALUE = 1;
    internal const int NOTE_MAX_LENGTH = 500;
    internal const int DECISION_REASON_MAX_LENGTH = 500;
    internal const int STATUS_MAX_LENGTH = 16;

    public Guid GrantRequestId { get; set; }
    public Guid AccountId { get; set; }
    public UsageOperation Operation { get; set; }
    public GrantKind Kind { get; set; }
    public UsageUnit? Unit { get; set; }
    public long? Quantity { get; set; }
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
