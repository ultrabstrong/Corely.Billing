using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Processors;
using Corely.Billing.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Corely.Billing.UnitTests.Quota.Processors;

public class QuotaProcessorTelemetryDecoratorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId1 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GrantId2 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IQuotaProcessor> _inner = new();
    private readonly Mock<IBillingTelemetry> _telemetry = new();

    private QuotaProcessorTelemetryDecorator Decorator() =>
        new(
            _inner.Object,
            NullLogger<QuotaProcessorTelemetryDecorator>.Instance,
            _telemetry.Object
        );

    private void ReserveReturns(ReserveQuotaResult result) =>
        _inner
            .Setup(p =>
                p.ReserveAsync(It.IsAny<ReserveQuotaRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(result);

    private void SettleReturns(SettleQuotaResult result) =>
        _inner
            .Setup(p =>
                p.SettleAsync(It.IsAny<SettleQuotaRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(result);

    private static ReserveQuotaRequest ReserveRequest =>
        new(AccountId, TestUsage.Extraction, TestUsage.Page, 1, "prov");

    private static SettleQuotaRequest SettleRequest =>
        new(AccountId, TestUsage.Extraction, TestUsage.Page, 5);

    [Fact]
    public async Task ReserveAsync_CountsAGrantFound_ForASuccessfulReservation()
    {
        ReserveReturns(
            new ReserveQuotaResult(
                ReserveQuotaResultCode.Success,
                "",
                [new GrantShare(GrantId1, 1)]
            )
        );

        await Decorator().ReserveAsync(ReserveRequest);

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Quota.GRANT_FOUND), Times.Once);
        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Quota.RESERVATION_SPANNED_GRANTS),
            Times.Never
        );
    }

    [Fact]
    public async Task ReserveAsync_CountsASpan_ForAReservationCoveringTwoGrants()
    {
        // Spanning succeeds silently, so without a counter a mismatch between grant sizes and work
        // sizes is invisible.
        ReserveReturns(
            new ReserveQuotaResult(
                ReserveQuotaResultCode.Success,
                "",
                [new GrantShare(GrantId1, 1), new GrantShare(GrantId2, 99)]
            )
        );

        await Decorator().ReserveAsync(ReserveRequest);

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Quota.RESERVATION_SPANNED_GRANTS),
            Times.Once
        );
    }

    [Fact]
    public async Task ReserveAsync_CountsAMiss_ForInsufficientQuota()
    {
        ReserveReturns(new ReserveQuotaResult(ReserveQuotaResultCode.InsufficientQuotaError, ""));

        await Decorator().ReserveAsync(ReserveRequest);

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Quota.GRANT_NOT_FOUND), Times.Once);
    }

    [Fact]
    public async Task SettleAsync_CountsAnOverdraft_ForWorkNoGrantHadRoomFor()
    {
        SettleReturns(
            new SettleQuotaResult(SettleQuotaResultCode.Success, "", 500, Overdrawn: true)
        );

        await Decorator().SettleAsync(SettleRequest);

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Quota.GRANT_OVERDRAWN), Times.Once);
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Quota.SETTLED_QUANTITY, 500),
            Times.Once
        );
    }

    [Fact]
    public async Task SettleAsync_CountsNoOverdraft_ForWorkThatFitted()
    {
        SettleReturns(new SettleQuotaResult(SettleQuotaResultCode.Success, "", 5));

        await Decorator().SettleAsync(SettleRequest);

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Quota.GRANT_OVERDRAWN), Times.Never);
    }

    [Fact]
    public async Task SettleAsync_RecordsNothing_ForAFailedSettle()
    {
        SettleReturns(new SettleQuotaResult(SettleQuotaResultCode.NotRecordedError, "no"));

        await Decorator().SettleAsync(SettleRequest);

        _telemetry.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0.05, true)]
    [InlineData(0.5, false)]
    [InlineData(null, false)]
    public async Task SettleAsync_CountsRunningLow_ForAccountUnderTenPercent(
        double? remaining,
        bool expectCounted
    )
    {
        SettleReturns(
            new SettleQuotaResult(SettleQuotaResultCode.Success, "", 5, RemainingRatio: remaining)
        );

        await Decorator().SettleAsync(SettleRequest);

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Quota.GRANTS_RUNNING_LOW),
            expectCounted ? Times.Once : Times.Never
        );
    }
}
