using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Models;

public class Grant
{
    public Guid GrantId { get; set; }
    public Guid AccountId { get; set; }
    public UsageOperation Operation { get; set; }
    public UsageUnit Unit { get; set; }
    public long Quantity { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
