using Corely.Billing.Usage;
using Corely.DataAccess.Interfaces.Entities;

namespace Corely.Billing.Prices.Entities;

internal class UsagePriceEntity : IHasCreatedUtc, IHasModifiedUtc
{
    public UsageOperation Operation { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }
}
