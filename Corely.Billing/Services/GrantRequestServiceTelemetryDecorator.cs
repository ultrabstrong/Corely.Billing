using Corely.Billing.Extensions;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Models;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Services;

internal class GrantRequestServiceTelemetryDecorator(
    IGrantRequestService inner,
    ILogger<GrantRequestServiceTelemetryDecorator> logger
) : IGrantRequestService
{
    private readonly IGrantRequestService _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<GrantRequestServiceTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );

    public Task<RequestGrantResult> RequestGrantAsync(
        RequestGrantRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestService),
            request,
            () => _inner.RequestGrantAsync(request, ct),
            logResult: true
        );

    public Task<RetrieveSingleResult<GrantRequest>> GetGrantRequestAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestService),
            grantRequestId,
            () => _inner.GetGrantRequestAsync(accountId, grantRequestId, ct)
        );

    public Task<RetrieveListResult<GrantRequest>> ListGrantRequestsAsync(
        ListGrantRequestsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestService),
            request,
            () => _inner.ListGrantRequestsAsync(request, authorizedResourceIds, ct)
        );

    public Task<ApproveGrantRequestResult> ApproveGrantRequestAsync(
        ApproveGrantRequestRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestService),
            request,
            () => _inner.ApproveGrantRequestAsync(request, ct),
            logResult: true
        );

    public Task<DeclineGrantRequestResult> DeclineGrantRequestAsync(
        DeclineGrantRequestRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestService),
            request,
            () => _inner.DeclineGrantRequestAsync(request, ct),
            logResult: true
        );
}
