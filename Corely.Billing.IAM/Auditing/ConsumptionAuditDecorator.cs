using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Audits.Models;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Enums;

namespace Corely.Billing.IAM.Auditing;

internal sealed class ConsumptionAuditDecorator(
    IConsumptionService inner,
    IAuditProvider auditProvider
) : IConsumptionService
{
    private const string SERVICE = nameof(IConsumptionService);

    public Task<RetrieveSingleResult<long>> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            Read(request.AccountId),
            () => inner.GetConsumptionTotalAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveSingleResult<List<ConsumptionSeries>>> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            Read(request.AccountId),
            () => inner.GetConsumptionTimeSeriesAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveListResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            Read(request.AccountIds is [var only] ? only : null),
            () => inner.ListConsumptionEventsAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveSingleResult<List<string>>> ListProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            Read(accountId),
            () => inner.ListProvidersAsync(accountId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveSingleResult<DateTime?>> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            Read(accountId),
            () => inner.GetEarliestConsumptionAsync(accountId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveSingleResult<int>> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            Read(accountId),
            () => inner.CountAbandonedReservationsAsync(accountId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    private static AuditCall Read(Guid? accountId) =>
        new(SERVICE, AuthAction.Read, BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE)
        {
            AccountId = accountId,
        };
}
