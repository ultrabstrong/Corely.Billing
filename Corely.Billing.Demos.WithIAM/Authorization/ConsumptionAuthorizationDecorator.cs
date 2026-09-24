using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;

namespace Corely.Billing.Demos.WithIAM.Authorization;

internal sealed class ConsumptionAuthorizationDecorator(
    IConsumptionService inner,
    IAuthorizationProvider authorization
) : IConsumptionService
{
    private const string DENIED = "Not authorized";

    public Task<RetrieveSingleResult<long>> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    ) => ReadAsync(() => inner.GetConsumptionTotalAsync(request, ct));

    public Task<RetrieveSingleResult<List<GrantTotalConsumptions>>> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    ) => ReadAsync(() => inner.GetGrantConsumptionTotalsAsync(accountId, grantIds, ct));

    public Task<
        RetrieveSingleResult<List<ConsumptionTimeBucketData>>
    > GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    ) => ReadAsync(() => inner.GetConsumptionTimeSeriesAsync(request, ct));

    public async Task<RetrieveListResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    ) =>
        await AllowedAsync()
            ? await inner.ListConsumptionEventsAsync(request, ct)
            : new RetrieveListResult<ConsumptionEvent>(
                RetrieveResultCode.UnauthorizedError,
                DENIED,
                null
            );

    public Task<RetrieveSingleResult<List<string>>> ListProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => ReadAsync(() => inner.ListProvidersAsync(accountId, ct));

    public Task<RetrieveSingleResult<DateTime?>> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => ReadAsync(() => inner.GetEarliestConsumptionAsync(accountId, ct));

    public Task<RetrieveSingleResult<int>> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => ReadAsync(() => inner.CountAbandonedReservationsAsync(accountId, ct));

    private async Task<RetrieveSingleResult<T>> ReadAsync<T>(
        Func<Task<RetrieveSingleResult<T>>> read
    ) =>
        await AllowedAsync()
            ? await read()
            : new RetrieveSingleResult<T>(RetrieveResultCode.UnauthorizedError, DENIED, default);

    private Task<bool> AllowedAsync() =>
        authorization.IsAuthorizedAsync(AuthAction.Read, DemoUsage.USAGE_RESOURCE);
}
