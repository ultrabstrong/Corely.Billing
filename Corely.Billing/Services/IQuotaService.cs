using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Services;

public interface IQuotaService
{
    Task<GetBalanceResult> GetBalanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    );

    Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    );

    Task<SettleQuotaResult> SettleAsync(SettleQuotaRequest request, CancellationToken ct = default);

    Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    );

    Task<GetAllowanceResult> GetAllowanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    );

    Task<HasFeatureResult> HasFeatureAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    );

    Task<GetCapacityResult> GetCapacityAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    );

    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<ModifyResult> ReleaseRegistrationAsync(
        Guid accountId,
        Guid registrationId,
        CancellationToken ct = default
    );

    Task<MoveRegistrationsResult> MoveRegistrationsAsync(
        MoveRegistrationsRequest request,
        CancellationToken ct = default
    );

    Task<RetrieveListResult<Registration>> ListRegistrationsAsync(
        ListRegistrationsRequest request,
        CancellationToken ct = default
    );
}
