using Corely.Billing;
using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Corely.Billing.UnitTests.Quota.Services;

public class QuotaTelemetryDecoratorTests
{
    private static readonly Guid TestAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId1 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GrantId2 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IQuotaService> _inner = new();
    private readonly Mock<IBillingTelemetry> _telemetry = new();

    private QuotaTelemetryDecorator Decorator() =>
        new(_inner.Object, NullLogger<QuotaTelemetryDecorator>.Instance, _telemetry.Object);

    private void ReserveReturns(ReserveQuotaResult result) =>
        _inner
            .Setup(s =>
                s.ReserveAsync(It.IsAny<ReserveQuotaRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(result);

    private void SettleReturns(SettleQuotaResult result) =>
        _inner
            .Setup(s =>
                s.SettleAsync(It.IsAny<SettleQuotaRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(result);

    private static ReserveQuotaRequest ReserveRequest =>
        new(TestAccountId, TestUsage.Extraction, TestUsage.Page, 1, "prov");

    private static SettleQuotaRequest SettleRequest =>
        new(TestAccountId, TestUsage.Extraction, TestUsage.Page, 5);

    [Fact]
    public async Task ReserveAsync_CountsAGrantFound_ForASuccessfulReservation()
    {
        ReserveReturns(
            new ReserveQuotaResult(
                ReserveQuotaResultCode.Success,
                null,
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
        // The rate of these is what says whether grant sizes and document sizes are mismatched --
        // and it is invisible without a counter, because spanning succeeds silently.
        ReserveReturns(
            new ReserveQuotaResult(
                ReserveQuotaResultCode.Success,
                null,
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
        ReserveReturns(new ReserveQuotaResult(ReserveQuotaResultCode.InsufficientQuota, null));

        await Decorator().ReserveAsync(ReserveRequest);

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Quota.GRANT_NOT_FOUND), Times.Once);
    }

    [Fact]
    public async Task SettleAsync_CountsAnOverdraft_ForWorkNoGrantHadRoomFor()
    {
        // Every overdraft is work delivered that nothing had quota for. The count per format is the
        // evidence that a pre-flight page count is worth building, and which format to build it for.
        SettleReturns(
            new SettleQuotaResult(SettleQuotaResultCode.Success, null, 500, Overdrawn: true)
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
        SettleReturns(new SettleQuotaResult(SettleQuotaResultCode.Success, null, 5));

        await Decorator().SettleAsync(SettleRequest);

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Quota.GRANT_OVERDRAWN), Times.Never);
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
            new SettleQuotaResult(SettleQuotaResultCode.Success, null, 5, RemainingRatio: remaining)
        );

        await Decorator().SettleAsync(SettleRequest);

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Quota.GRANTS_RUNNING_LOW),
            expectCounted ? Times.Once : Times.Never
        );
    }
}
