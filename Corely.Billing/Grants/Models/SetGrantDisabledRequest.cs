namespace Corely.Billing.Grants.Models;

public record SetGrantDisabledRequest(Guid AccountId, Guid GrantId, bool Disabled);
