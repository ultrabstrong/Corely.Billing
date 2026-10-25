using Corely.Billing.Grants.Models;

namespace Corely.Billing.Registrations.Processors;

internal interface IRegistrationSelectionPolicy
{
    Grant? Pick(IReadOnlyList<Grant> grants, IReadOnlyDictionary<Guid, long> inUseByGrant);
}
