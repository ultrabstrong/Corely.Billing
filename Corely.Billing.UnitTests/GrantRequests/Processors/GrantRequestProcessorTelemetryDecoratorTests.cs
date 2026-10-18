using Corely.Billing.GrantRequests.Models;
using Corely.Billing.GrantRequests.Processors;
using Corely.Billing.Telemetry;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.GrantRequests.Processors;

public class GrantRequestProcessorTelemetryDecoratorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RequestId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IGrantRequestProcessor> _inner = new();
    private readonly Mock<IBillingTelemetry> _telemetry = new();
    private readonly GrantRequestProcessorTelemetryDecorator _decorator;

    public GrantRequestProcessorTelemetryDecoratorTests()
    {
        _decorator = new GrantRequestProcessorTelemetryDecorator(
            _inner.Object,
            Mock.Of<ILogger<GrantRequestProcessorTelemetryDecorator>>(),
            _telemetry.Object
        );
    }

    [Theory]
    [InlineData(RequestGrantResultCode.Success, 1)]
    [InlineData(RequestGrantResultCode.AlreadyPendingError, 0)]
    public async Task RequestGrantAsync_CountsOnlySuccess_ForEachOutcome(
        RequestGrantResultCode code,
        int expected
    )
    {
        _inner
            .Setup(p => p.RequestGrantAsync(It.IsAny<RequestGrantRequest>(), default))
            .ReturnsAsync(new RequestGrantResult(code, "", RequestId));

        await _decorator.RequestGrantAsync(
            new(AccountId, TestUsage.Extraction, TestUsage.Page, 250)
        );

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.GrantRequests.GRANT_REQUEST_CREATED),
            Times.Exactly(expected)
        );
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.GrantRequests.GRANT_REQUEST_QUANTITY, 250),
            Times.Exactly(expected)
        );
    }

    [Theory]
    [InlineData(DecideGrantRequestResultCode.Success, 1)]
    [InlineData(DecideGrantRequestResultCode.AlreadyDecidedError, 0)]
    public async Task ApproveGrantRequestAsync_CountsOnlySuccess_ForEachOutcome(
        DecideGrantRequestResultCode code,
        int expected
    )
    {
        _inner
            .Setup(p => p.ApproveGrantRequestAsync(It.IsAny<ApproveGrantRequestRequest>(), default))
            .ReturnsAsync(new ApproveGrantRequestResult(code, "", Guid.Empty));

        await _decorator.ApproveGrantRequestAsync(
            new(AccountId, RequestId, From, From.AddYears(1))
        );

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.GrantRequests.GRANT_REQUEST_APPROVED),
            Times.Exactly(expected)
        );
    }

    [Theory]
    [InlineData(DecideGrantRequestResultCode.Success, 1)]
    [InlineData(DecideGrantRequestResultCode.NotFoundError, 0)]
    public async Task DeclineGrantRequestAsync_CountsOnlySuccess_ForEachOutcome(
        DecideGrantRequestResultCode code,
        int expected
    )
    {
        _inner
            .Setup(p => p.DeclineGrantRequestAsync(It.IsAny<DeclineGrantRequestRequest>(), default))
            .ReturnsAsync(new DeclineGrantRequestResult(code, ""));

        await _decorator.DeclineGrantRequestAsync(new(AccountId, RequestId));

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.GrantRequests.GRANT_REQUEST_DECLINED),
            Times.Exactly(expected)
        );
    }
}
