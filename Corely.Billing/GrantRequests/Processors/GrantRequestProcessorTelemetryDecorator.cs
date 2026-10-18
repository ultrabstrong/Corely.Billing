using Corely.Billing.Extensions;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Models;
using Corely.Billing.Telemetry;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.GrantRequests.Processors;

internal class GrantRequestProcessorTelemetryDecorator(
    IGrantRequestProcessor inner,
    ILogger<GrantRequestProcessorTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IGrantRequestProcessor
{
    private readonly IGrantRequestProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<GrantRequestProcessorTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );
    private readonly IBillingTelemetry _telemetry = telemetry.ThrowIfNull(nameof(telemetry));

    public async Task<RequestGrantResult> RequestGrantAsync(
        RequestGrantRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestProcessor),
            request,
            () => _inner.RequestGrantAsync(request, ct),
            logResult: true
        );
        if (result.ResultCode == RequestGrantResultCode.Success)
        {
            _telemetry.Increment(BillingMetricNames.GrantRequests.GRANT_REQUEST_CREATED);
            _telemetry.Record(
                BillingMetricNames.GrantRequests.GRANT_REQUEST_QUANTITY,
                request.Quantity
            );
        }
        return result;
    }

    public Task<RetrieveSingleResult<GrantRequest>> GetGrantRequestAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestProcessor),
            grantRequestId,
            () => _inner.GetGrantRequestAsync(accountId, grantRequestId, ct)
        );

    public Task<RetrieveListResult<GrantRequest>> ListGrantRequestsAsync(
        ListGrantRequestsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestProcessor),
            request,
            () => _inner.ListGrantRequestsAsync(request, authorizedResourceIds, ct)
        );

    public async Task<ApproveGrantRequestResult> ApproveGrantRequestAsync(
        ApproveGrantRequestRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestProcessor),
            request,
            () => _inner.ApproveGrantRequestAsync(request, ct),
            logResult: true
        );
        if (result.ResultCode == DecideGrantRequestResultCode.Success)
            _telemetry.Increment(BillingMetricNames.GrantRequests.GRANT_REQUEST_APPROVED);
        return result;
    }

    public async Task<DeclineGrantRequestResult> DeclineGrantRequestAsync(
        DeclineGrantRequestRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantRequestProcessor),
            request,
            () => _inner.DeclineGrantRequestAsync(request, ct),
            logResult: true
        );
        if (result.ResultCode == DecideGrantRequestResultCode.Success)
            _telemetry.Increment(BillingMetricNames.GrantRequests.GRANT_REQUEST_DECLINED);
        return result;
    }
}
