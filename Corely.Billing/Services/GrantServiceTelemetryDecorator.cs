using Corely.Billing.Extensions;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Services;

internal class GrantServiceTelemetryDecorator(
    IGrantService inner,
    ILogger<GrantServiceTelemetryDecorator> logger
) : IGrantService
{
    private readonly IGrantService _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<GrantServiceTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );

    public Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantService),
            request,
            () => _inner.CreateGrantAsync(request, ct),
            logResult: true
        );

    public Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantService),
            grantId,
            () => _inner.GetGrantAsync(accountId, grantId, ct)
        );

    public Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantService),
            request,
            () => _inner.ListGrantsAsync(request, ct)
        );

    public Task<ModifyResult> UpdateGrantAsync(
        UpdateGrantRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantService),
            request,
            () => _inner.UpdateGrantAsync(request, ct),
            logResult: true
        );

    public Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantService),
            grantId,
            () => _inner.DeleteGrantAsync(accountId, grantId, ct),
            logResult: true
        );
}
