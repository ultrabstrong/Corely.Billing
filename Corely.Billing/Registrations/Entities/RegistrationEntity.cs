using Corely.Billing.Usage;
using Corely.DataAccess.Interfaces.Entities;

namespace Corely.Billing.Registrations.Entities;

internal class RegistrationEntity : IHasCreatedUtc
{
    public Guid RegistrationId { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Guid AccountId { get; set; }
    public Guid GrantId { get; set; }
    public UsageOperation Operation { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public DateTime RegisteredUtc { get; set; }
    public DateTime? ReleasedUtc { get; set; }
}
