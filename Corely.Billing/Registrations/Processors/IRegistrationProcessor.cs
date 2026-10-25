using Corely.Billing.Models;
using Corely.Billing.Registrations.Models;

namespace Corely.Billing.Registrations.Processors;

internal interface IRegistrationProcessor
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<ModifyResult> ReleaseAsync(
        Guid accountId,
        Guid registrationId,
        CancellationToken ct = default
    );

    Task<int> ReleaseAllOnGrantAsync(Guid accountId, Guid grantId, CancellationToken ct = default);

    Task<MoveRegistrationsResult> MoveAsync(
        MoveRegistrationsRequest request,
        CancellationToken ct = default
    );

    Task<RetrieveListResult<Registration>> ListAsync(
        ListRegistrationsRequest request,
        CancellationToken ct = default
    );

    Task<Dictionary<Guid, long>> CountInUseAsync(
        IReadOnlyCollection<Guid> grantIds,
        CancellationToken ct = default
    );
}
