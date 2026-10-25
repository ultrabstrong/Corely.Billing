using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.DataAccess.Interfaces.Entities;

namespace Corely.Billing.GrantRequests.Entities;

internal class GrantRequestEntity : IHasCreatedUtc
{
    public Guid GrantRequestId { get; set; }
    public DateTime CreatedUtc { get; set; }

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
    public string? TagsJson { get; set; }
}
