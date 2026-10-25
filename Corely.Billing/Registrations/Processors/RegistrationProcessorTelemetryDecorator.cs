using Corely.Billing.Extensions;
using Corely.Billing.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Telemetry;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Registrations.Processors;

internal class RegistrationProcessorTelemetryDecorator(
    IRegistrationProcessor inner,
    ILogger<RegistrationProcessorTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IRegistrationProcessor
{
    private readonly IRegistrationProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<RegistrationProcessorTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );
    private readonly IBillingTelemetry _telemetry = telemetry.ThrowIfNull(nameof(telemetry));

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(RegistrationProcessor),
            request,
            () => _inner.RegisterAsync(request, ct),
            logResult: true
        );
        _telemetry.Increment(
            result.ResultCode == RegisterResultCode.Registered
                ? BillingMetricNames.Registrations.REGISTERED
                : BillingMetricNames.Registrations.REFUSED
        );
        return result;
    }

    public async Task<ModifyResult> ReleaseAsync(
        Guid accountId,
        Guid registrationId,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(RegistrationProcessor),
            registrationId,
            () => _inner.ReleaseAsync(accountId, registrationId, ct),
            logResult: true
        );
        if (result.ResultCode == ModifyResultCode.Success)
            _telemetry.Increment(BillingMetricNames.Registrations.RELEASED);
        return result;
    }

    public async Task<MoveRegistrationsResult> MoveAsync(
        MoveRegistrationsRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(RegistrationProcessor),
            request,
            () => _inner.MoveAsync(request, ct),
            logResult: true
        );
        if (result.Moved.Count > 0)
            _telemetry.Record(BillingMetricNames.Registrations.MOVED, result.Moved.Count);
        return result;
    }

    public Task<RetrieveListResult<Registration>> ListAsync(
        ListRegistrationsRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(RegistrationProcessor),
            request,
            () => _inner.ListAsync(request, ct)
        );

    public Task<Dictionary<Guid, long>> CountInUseAsync(
        IReadOnlyCollection<Guid> grantIds,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(RegistrationProcessor),
            grantIds.Count,
            () => _inner.CountInUseAsync(grantIds, ct)
        );
}
