using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Processors;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Microsoft.Extensions.Logging.Abstractions;

namespace Corely.Billing.UnitTests.Quota.Processors;

public class QuotaProcessorTests
{
    private static readonly Guid GrantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AccountId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly ServiceFactory _serviceFactory = new();
    private readonly Mock<IGrantProcessor> _grants = new();
    private readonly Mock<IConsumptionProcessor> _consumption = new();
    private readonly Mock<IConsumptionReportProcessor> _consumptionReport = new();

    public QuotaProcessorTests()
    {
        HaveGrants();
        HaveTotals();
        HaveOutstanding();

        _consumption
            .Setup(p => p.ReserveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReserveConsumptionResult(ReserveConsumptionResultCode.Success, ""));

        _consumption
            .Setup(p =>
                p.SettleAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<Guid, long>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    Guid _,
                    UsageOperation _,
                    UsageUnit _,
                    IReadOnlyDictionary<Guid, long> split,
                    CancellationToken _
                ) =>
                    new ResolveConsumptionResult(
                        ResolveConsumptionResultCode.Success,
                        "",
                        split.Values.Sum(),
                        split.Count
                    )
            );

        _consumption
            .Setup(p =>
                p.ReleaseAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ResolveConsumptionResult(ResolveConsumptionResultCode.Success, ""));
    }

    private DateTime Now => _serviceFactory.TimeProvider.GetUtcNow().UtcDateTime;

    private QuotaProcessor Processor() =>
        new(
            _grants.Object,
            _consumption.Object,
            _consumptionReport.Object,
            new ExpiringFirstGrantSelectionPolicy(),
            _serviceFactory.GetRequiredService<IValidationProvider>(),
            _serviceFactory.TimeProvider,
            NullLogger<QuotaProcessor>.Instance
        );

    [Fact]
    public async Task ReserveAsync_ReportsNoGrant_ForAnAccountWithNone()
    {
        var result = await Processor().ReserveAsync(Reserve(1));

        Assert.Equal(ReserveQuotaResultCode.NoGrantAvailableError, result.ResultCode);
    }

    [Fact]
    public async Task ReserveAsync_ReportsValidationError_ForAnUnregisteredUnit()
    {
        HaveGrants(Grant(GrantId1, 100));

        var result = await Processor()
            .ReserveAsync(Reserve(1) with { Unit = TestUsage.UnregisteredUnit });

        Assert.Equal(ReserveQuotaResultCode.ValidationError, result.ResultCode);
        VerifyNothingReserved();
    }

    [Fact]
    public async Task ReserveAsync_WritesOneReservation_ForAGrantWithRoom()
    {
        HaveGrants(Grant(GrantId1, 100));

        var result = await Processor().ReserveAsync(Reserve(10));

        Assert.Equal(ReserveQuotaResultCode.Success, result.ResultCode);
        Assert.Equal(new GrantShare(GrantId1, 10), Assert.Single(result.Shares!));
        VerifyReserved(GrantId1, 10);
    }

    [Fact]
    public async Task ReserveAsync_WritesOneReservationPerGrant_ForWorkThatSpansAGrantEdge()
    {
        HaveGrants(Grant(GrantId1, 100, expiresInDays: 2), Grant(GrantId2, 1000));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 99));

        var result = await Processor().ReserveAsync(Reserve(500));

        Assert.Equal(ReserveQuotaResultCode.Success, result.ResultCode);
        Assert.Equal(2, result.Shares!.Count);
        VerifyReserved(GrantId1, 1);
        VerifyReserved(GrantId2, 499);
    }

    [Fact]
    public async Task ReserveAsync_RefusesOnTheTotalAcrossGrants_ForInsufficientQuota()
    {
        HaveGrants(Grant(GrantId1, 10), Grant(GrantId2, 5));

        var result = await Processor().ReserveAsync(Reserve(100));

        Assert.Equal(ReserveQuotaResultCode.InsufficientQuotaError, result.ResultCode);
        VerifyNothingReserved();
    }

    [Fact]
    public async Task ReserveAsync_ReportsNotRecorded_ForAReservationThatCannotBeWritten()
    {
        HaveGrants(Grant(GrantId1, 100));
        _consumption
            .Setup(p => p.ReserveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ReserveConsumptionResult(ReserveConsumptionResultCode.NotRecordedError, "no")
            );

        var result = await Processor().ReserveAsync(Reserve(10));

        Assert.Equal(ReserveQuotaResultCode.NotRecordedError, result.ResultCode);
    }

    [Fact]
    public async Task SettleAsync_ChargesTheOneGrant_ForWorkThatFitsWhereItWasReserved()
    {
        HaveGrants(Grant(GrantId1, 100));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 1));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Processor().SettleAsync(Settle(20));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        Assert.False(result.Overdrawn);
        Assert.Equal(20, result.SettledQuantity);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 20 });
    }

    [Fact]
    public async Task SettleAsync_SpillsIntoTheNextGrant_ForWorkLargerThanTheOneItWasHeldOn()
    {
        HaveGrants(Grant(GrantId1, 100, expiresInDays: 2), Grant(GrantId2, 1000));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 100));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Processor().SettleAsync(Settle(500));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 1, [GrantId2] = 499 });
    }

    [Fact]
    public async Task SettleAsync_DoesNotCompeteWithItsOwnHold_ForAGrantItAlreadyReserved()
    {
        HaveGrants(Grant(GrantId1, 10, expiresInDays: 2), Grant(GrantId2, 1000));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 10));
        HaveOutstanding(Outstanding(GrantId1, 10));

        await Processor().SettleAsync(Settle(10));

        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 10 });
    }

    [Fact]
    public async Task SettleAsync_OverdrawsTheLastGrant_ForWorkNoGrantHadRoomFor()
    {
        HaveGrants(Grant(GrantId1, 10));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 1));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Processor().SettleAsync(Settle(500));

        Assert.True(result.Overdrawn);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 500 });
    }

    [Fact]
    public async Task SettleAsync_ChargesTheReservedGrant_ForAnAccountWhoseGrantsAllExpired()
    {
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Processor().SettleAsync(Settle(7));

        Assert.True(result.Overdrawn);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 7 });
    }

    [Theory]
    [InlineData(20, 0.8)]
    [InlineData(95, 0.05)]
    [InlineData(500, 0.0)]
    public async Task SettleAsync_ReportsWhatIsLeft_ForTheAccountsLiveGrants(
        long charged,
        double expected
    )
    {
        HaveGrants(Grant(GrantId1, 100));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 1));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Processor().SettleAsync(Settle(charged));

        Assert.Equal(expected, result.RemainingRatio!.Value, precision: 6);
    }

    [Fact]
    public async Task SettleAsync_WritesNothing_ForAReplayWithNoOutstandingReservations()
    {
        var result = await Processor().SettleAsync(Settle(20));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        VerifySettled(new Dictionary<Guid, long>());
    }

    [Fact]
    public async Task SettleAsync_SettlesNothing_ForNoAmbientOperationContext()
    {
        HaveGrants(Grant(GrantId1, 100));
        _consumption
            .Setup(p =>
                p.ListOutstandingReservationsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((List<ConsumptionEvent>?)null);

        var result = await Processor().SettleAsync(Settle(20));

        Assert.Equal(SettleQuotaResultCode.NotRecordedError, result.ResultCode);
        _consumption.Verify(
            p =>
                p.SettleAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<Guid, long>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task SettleAsync_ReportsNotRecorded_ForALedgerThatCannotBeUpdated()
    {
        HaveGrants(Grant(GrantId1, 100));
        HaveOutstanding(Outstanding(GrantId1, 1));
        _consumption
            .Setup(p =>
                p.SettleAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<Guid, long>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ResolveConsumptionResult(ResolveConsumptionResultCode.NotRecordedError, "no")
            );

        var result = await Processor().SettleAsync(Settle(20));

        Assert.Equal(SettleQuotaResultCode.NotRecordedError, result.ResultCode);
        Assert.Null(result.RemainingRatio);
    }

    [Fact]
    public async Task ReleaseAsync_GivesTheHoldBack_ForAFailedStep()
    {
        var result = await Processor()
            .ReleaseAsync(new ReleaseQuotaRequest(AccountId, TestUsage.Extraction, TestUsage.Page));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        _consumption.Verify(
            p =>
                p.ReleaseAsync(
                    AccountId,
                    TestUsage.Extraction,
                    TestUsage.Page,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsAvailable_ForAGrantWithRoom()
    {
        HaveGrants(Grant(GrantId1, 100));

        Assert.Equal(QuotaAvailability.Available, await Availability());
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsExhausted_ForAnAccountWithNoGrants() =>
        Assert.Equal(QuotaAvailability.Exhausted, await Availability());

    [Fact]
    public async Task GetAvailabilityAsync_ReportsExhausted_ForGrantsWithNothingLeft()
    {
        HaveGrants(Grant(GrantId1, 10));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 10));

        Assert.Equal(QuotaAvailability.Exhausted, await Availability());
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsUnknown_ForAGrantStoreThatThrows()
    {
        _grants
            .Setup(p =>
                p.ListActiveGrantsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("database is down"));

        Assert.Equal(QuotaAvailability.Unknown, await Availability());
    }

    [Fact]
    public async Task GetAvailabilityAsync_WritesNothing_ForAnyOutcome()
    {
        HaveGrants(Grant(GrantId1, 100));

        await Availability();

        VerifyNothingReserved();
    }

    private Task<QuotaAvailability> Availability() =>
        Processor().GetAvailabilityAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

    private void VerifyReserved(Guid grantId, long quantity) =>
        _consumption.Verify(
            p =>
                p.ReserveAsync(
                    It.Is<ConsumptionEvent>(e => e.GrantId == grantId && e.Quantity == quantity),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

    private void VerifyNothingReserved() =>
        _consumption.Verify(
            p => p.ReserveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

    private void VerifySettled(IReadOnlyDictionary<Guid, long> expected) =>
        _consumption.Verify(
            p =>
                p.SettleAsync(
                    AccountId,
                    TestUsage.Extraction,
                    TestUsage.Page,
                    It.Is<IReadOnlyDictionary<Guid, long>>(d =>
                        d.Count == expected.Count
                        && d.All(kv => expected.ContainsKey(kv.Key) && expected[kv.Key] == kv.Value)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

    private static ReserveQuotaRequest Reserve(long quantity) =>
        new(AccountId, TestUsage.Extraction, TestUsage.Page, quantity, "prov");

    private static SettleQuotaRequest Settle(long actual) =>
        new(AccountId, TestUsage.Extraction, TestUsage.Page, actual);

    private void HaveGrants(params Grant[] grants) =>
        _grants
            .Setup(p =>
                p.ListActiveGrantsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([.. grants]);

    private void HaveTotals(params GrantTotalConsumptions[] totals) =>
        _consumptionReport
            .Setup(p =>
                p.GetGrantConsumptionTotalsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([.. totals]);

    private void HaveOutstanding(params ConsumptionEvent[] outstanding) =>
        _consumption
            .Setup(p =>
                p.ListOutstandingReservationsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([.. outstanding]);

    private Grant Grant(Guid grantId, long quantity, int expiresInDays = 30) =>
        new()
        {
            GrantId = grantId,
            AccountId = AccountId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = quantity,
            ValidFromUtc = Now.AddDays(-1),
            ValidToUtc = Now.AddDays(expiresInDays),
        };

    private ConsumptionEvent Outstanding(Guid grantId, long quantity) =>
        new()
        {
            AccountId = AccountId,
            GrantId = grantId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = quantity,
            Provider = "prov",
            UtcTimestamp = Now,
        };
}
