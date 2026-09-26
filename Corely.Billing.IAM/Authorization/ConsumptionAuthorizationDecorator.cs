using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class ConsumptionAuthorizationDecorator(
    IConsumptionService inner,
    IAuthorizationProvider authorizationProvider
) : IConsumptionService
{
    public Task<RetrieveSingleResult<long>> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    ) =>
        ReadAsync(
            request.AccountId,
            "Unauthorized to read consumption total",
            () => inner.GetConsumptionTotalAsync(request, ct)
        );

    public Task<RetrieveSingleResult<List<GrantTotalConsumptions>>> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    ) =>
        ReadAsync(
            accountId,
            "Unauthorized to read grant consumption totals",
            () => inner.GetGrantConsumptionTotalsAsync(accountId, grantIds, ct)
        );

    public Task<
        RetrieveSingleResult<List<ConsumptionTimeBucketData>>
    > GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    ) =>
        ReadAsync(
            request.AccountId,
            "Unauthorized to read consumption time series",
            () => inner.GetConsumptionTimeSeriesAsync(request, ct)
        );

    public async Task<RetrieveListResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(request.AccountId)
            ? await inner.ListConsumptionEventsAsync(request, ct)
            : new RetrieveListResult<ConsumptionEvent>(
                RetrieveResultCode.UnauthorizedError,
                "Unauthorized to list consumption events",
                null
            );

    public Task<RetrieveSingleResult<List<string>>> ListProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        ReadAsync(
            accountId,
            "Unauthorized to list consumption providers",
            () => inner.ListProvidersAsync(accountId, ct)
        );

    public Task<RetrieveSingleResult<DateTime?>> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        ReadAsync(
            accountId,
            "Unauthorized to read earliest consumption",
            () => inner.GetEarliestConsumptionAsync(accountId, ct)
        );

    public Task<RetrieveSingleResult<int>> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        ReadAsync(
            accountId,
            "Unauthorized to count abandoned reservations",
            () => inner.CountAbandonedReservationsAsync(accountId, ct)
        );

    private async Task<RetrieveSingleResult<T>> ReadAsync<T>(
        Guid accountId,
        string denied,
        Func<Task<RetrieveSingleResult<T>>> read
    ) =>
        await IsAuthorizedAsync(accountId)
            ? await read()
            : new RetrieveSingleResult<T>(RetrieveResultCode.UnauthorizedError, denied, default);

    private async Task<bool> IsAuthorizedAsync(Guid accountId) =>
        authorizationProvider.HasAccountContext(accountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Read,
            BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE
        );
}
