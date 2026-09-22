using Corely.Billing.Usage;
using Corely.DataAccess.Interfaces.Entities;

namespace Corely.Billing.Grants.Entities;

internal class GrantEntity : IHasCreatedUtc
{
    public Guid GrantId { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Guid AccountId { get; set; }
    public long Quantity { get; set; }
    public UsageUnit Unit { get; set; }
    public UsageOperation Operation { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public string? TagsJson { get; set; }
}
