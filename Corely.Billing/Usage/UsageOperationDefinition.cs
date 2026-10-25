using Corely.Billing.Grants.Models;

namespace Corely.Billing.Usage;

public sealed record UsageOperationDefinition(
    UsageOperation Operation,
    string DisplayName,
    GrantKind Kind,
    UsageUnit? Unit
)
{
    public bool Declares(GrantKind kind, UsageUnit? unit) => Kind == kind && Unit == unit;
}
