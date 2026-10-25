using Corely.Billing.Grants.Models;

namespace Corely.Billing.Registrations.Processors;

internal sealed class RegistrationSelectionPolicy : IRegistrationSelectionPolicy
{
    public Grant? Pick(IReadOnlyList<Grant> grants, IReadOnlyDictionary<Guid, long> inUseByGrant)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(inUseByGrant);

        return grants
            .Where(g =>
                g.Quantity is not { } seats || inUseByGrant.GetValueOrDefault(g.GrantId) < seats
            )
            .OrderByDescending(g => g.ValidToUtc)
            .ThenBy(g => g.Quantity is not null)
            .ThenBy(g => g.ValidFromUtc)
            .ThenBy(g => g.GrantId)
            .FirstOrDefault();
    }
}
