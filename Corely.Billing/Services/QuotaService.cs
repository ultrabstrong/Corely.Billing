using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Processors;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Registrations.Processors;
using Corely.Billing.Usage;
using Corely.Common.Extensions;

namespace Corely.Billing.Services;

internal class QuotaService(
    IQuotaProcessor quotaProcessor,
    IRegistrationProcessor registrationProcessor
) : IQuotaService
{
    private readonly IQuotaProcessor _quotaProcessor = quotaProcessor.ThrowIfNull(
        nameof(quotaProcessor)
    );
    private readonly IRegistrationProcessor _registrationProcessor =
        registrationProcessor.ThrowIfNull(nameof(registrationProcessor));

    public Task<GetBalanceResult> GetBalanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) => _quotaProcessor.GetBalanceAsync(accountId, operation, ct);

    public Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    ) => _quotaProcessor.ReserveAsync(request, ct);

    public Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    ) => _quotaProcessor.SettleAsync(request, ct);

    public Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    ) => _quotaProcessor.ReleaseAsync(request, ct);

    public Task<GetAllowanceResult> GetAllowanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) => _quotaProcessor.GetAllowanceAsync(accountId, operation, ct);

    public Task<HasFeatureResult> HasFeatureAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) => _quotaProcessor.HasFeatureAsync(accountId, operation, ct);

    public Task<GetCapacityResult> GetCapacityAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) => _quotaProcessor.GetCapacityAsync(accountId, operation, ct);

    public Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    ) => _registrationProcessor.RegisterAsync(request, ct);

    public Task<ModifyResult> ReleaseRegistrationAsync(
        Guid accountId,
        Guid registrationId,
        CancellationToken ct = default
    ) => _registrationProcessor.ReleaseAsync(accountId, registrationId, ct);

    public Task<MoveRegistrationsResult> MoveRegistrationsAsync(
        MoveRegistrationsRequest request,
        CancellationToken ct = default
    ) => _registrationProcessor.MoveAsync(request, ct);

    public Task<RetrieveListResult<Registration>> ListRegistrationsAsync(
        ListRegistrationsRequest request,
        CancellationToken ct = default
    ) => _registrationProcessor.ListAsync(request, ct);
}
