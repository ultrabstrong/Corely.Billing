using Corely.Billing.Consumption.Models;
using Corely.Billing.Extensions;
using Corely.Billing.Models;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Services;

internal class ConsumptionServiceTelemetryDecorator(
    IConsumptionService inner,
    ILogger<ConsumptionServiceTelemetryDecorator> logger
) : IConsumptionService
{
    private readonly IConsumptionService _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<ConsumptionServiceTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );

    public Task<RetrieveSingleResult<long>> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionService),
            request,
            () => _inner.GetConsumptionTotalAsync(request, ct)
        );

    public Task<RetrieveSingleResult<List<GrantTotalConsumptions>>> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionService),
            accountId,
            () => _inner.GetGrantConsumptionTotalsAsync(accountId, grantIds, ct)
        );

    public Task<
        RetrieveSingleResult<List<ConsumptionTimeBucketData>>
    > GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionService),
            request,
            () => _inner.GetConsumptionTimeSeriesAsync(request, ct)
        );

    public Task<RetrieveListResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionService),
            request,
            () => _inner.ListConsumptionEventsAsync(request, ct)
        );

    public Task<RetrieveSingleResult<List<string>>> ListProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionService),
            accountId,
            () => _inner.ListProvidersAsync(accountId, ct)
        );

    public Task<RetrieveSingleResult<DateTime?>> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionService),
            accountId,
            () => _inner.GetEarliestConsumptionAsync(accountId, ct)
        );

    public Task<RetrieveSingleResult<int>> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionService),
            accountId,
            () => _inner.CountAbandonedReservationsAsync(accountId, ct)
        );
}
