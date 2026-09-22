using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Services;
using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.UnitTests.Quota.Services;

/// <summary>
/// Holding quota, settling it against what the work cost, and giving it back.
/// </summary>
/// <remarks>
/// The real selection policy, substituted grant and consumption stores. What is under test is the
/// arithmetic between the two -- which grants a quantity draws on, and what settlement does when the
/// real number turns out larger than the hold -- and a substituted policy would prove none of it.
/// </remarks>
public class QuotaServiceTests
{
    private static readonly Guid GrantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AccountId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGrantReader> _grants = new();
    private readonly Mock<IConsumptionReader> _consumptionReader = new();
    private readonly Mock<IConsumptionWriter> _consumptionWriter = new();
    private readonly FakeTimeProvider _time = new(Now);

    public QuotaServiceTests()
    {
        HaveGrants();
        HaveTotals();
        HaveOutstanding();

        _consumptionWriter
            .Setup(w => w.ReserveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveConsumptionResult(SaveConsumptionResultCode.Success, null));

        _consumptionWriter
            .Setup(w =>
                w.SettleAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<Guid, long>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SettleConsumptionResult(SettleConsumptionResultCode.Success, null));

        _consumptionWriter
            .Setup(w =>
                w.ReleaseAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SettleConsumptionResult(SettleConsumptionResultCode.Success, null));
    }

    private QuotaService Service() =>
        new(
            _grants.Object,
            _consumptionReader.Object,
            _consumptionWriter.Object,
            new ExpiringFirstGrantSelectionPolicy(),
            _time,
            NullLogger<QuotaService>.Instance
        );

    [Fact]
    public async Task ReserveAsync_ReportsNoGrant_ForAnAccountWithNone()
    {
        HaveGrants();

        var result = await Service().ReserveAsync(Reserve(1));

        Assert.Equal(ReserveQuotaResultCode.NoGrantAvailable, result.ResultCode);
    }

    [Fact]
    public async Task ReserveAsync_WritesOneReservation_ForAGrantWithRoom()
    {
        HaveGrants(Grant(GrantId1, 100));

        var result = await Service().ReserveAsync(Reserve(10));

        Assert.Equal(ReserveQuotaResultCode.Success, result.ResultCode);
        var share = Assert.Single(result.Shares!);
        Assert.Equal(new GrantShare(GrantId1, 10), share);
        _consumptionWriter.Verify(
            w =>
                w.ReserveAsync(
                    It.Is<ConsumptionEvent>(e => e.GrantId == GrantId1 && e.Quantity == 10),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ReserveAsync_WritesOneReservationPerGrant_ForWorkThatSpansAGrantEdge()
    {
        // A grant with one page left used to be selected for a five-hundred-page document and
        // charged all five hundred. One row per grant is what makes the ledger add up.
        HaveGrants(Grant(GrantId1, 100, expiresInDays: 2), Grant(GrantId2, 1000));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 99));

        var result = await Service().ReserveAsync(Reserve(500));

        Assert.Equal(ReserveQuotaResultCode.Success, result.ResultCode);
        Assert.Equal(2, result.Shares!.Count);
        _consumptionWriter.Verify(
            w =>
                w.ReserveAsync(
                    It.Is<ConsumptionEvent>(e => e.GrantId == GrantId1 && e.Quantity == 1),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _consumptionWriter.Verify(
            w =>
                w.ReserveAsync(
                    It.Is<ConsumptionEvent>(e => e.GrantId == GrantId2 && e.Quantity == 499),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ReserveAsync_RefusesOnTheTotalAcrossGrants_ForInsufficientQuota()
    {
        // The honest meaning of "out of quota": not enough across every valid grant together, rather
        // than the one grant we happened to pick being too small.
        HaveGrants(Grant(GrantId1, 10), Grant(GrantId2, 5));

        var result = await Service().ReserveAsync(Reserve(100));

        Assert.Equal(ReserveQuotaResultCode.InsufficientQuota, result.ResultCode);
        _consumptionWriter.Verify(
            w => w.ReserveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ReserveAsync_ReportsNotRecorded_ForAReservationThatCannotBeWritten()
    {
        HaveGrants(Grant(GrantId1, 100));
        _consumptionWriter
            .Setup(w => w.ReserveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveConsumptionResult(SaveConsumptionResultCode.Failed, "no"));

        var result = await Service().ReserveAsync(Reserve(10));

        Assert.Equal(ReserveQuotaResultCode.NotRecorded, result.ResultCode);
    }

    [Fact]
    public async Task SettleAsync_ChargesTheOneGrant_ForWorkThatFitsWhereItWasReserved()
    {
        HaveGrants(Grant(GrantId1, 100));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 1));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Service().SettleAsync(Settle(20));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        Assert.False(result.Overdrawn);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 20 });
    }

    [Fact]
    public async Task SettleAsync_SpillsIntoTheNextGrant_ForWorkLargerThanTheOneItWasHeldOn()
    {
        // Reserve the floor, discover the truth. This is the case pre-flight page counting would
        // make rarer, not one it would remove: the count only becomes knowable after the provider
        // has run for anything that is not a PDF.
        HaveGrants(Grant(GrantId1, 100, expiresInDays: 2), Grant(GrantId2, 1000));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 100));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Service().SettleAsync(Settle(500));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 1, [GrantId2] = 499 });
    }

    [Fact]
    public async Task SettleAsync_DoesNotCompeteWithItsOwnHold_ForAGrantItAlreadyReserved()
    {
        // The hold is already inside the totals. Left there, a settlement would see its own reserved
        // page as consumed and push the charge onto the next grant for no reason.
        HaveGrants(Grant(GrantId1, 10, expiresInDays: 2), Grant(GrantId2, 1000));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 10));
        HaveOutstanding(Outstanding(GrantId1, 10));

        await Service().SettleAsync(Settle(10));

        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 10 });
    }

    [Fact]
    public async Task SettleAsync_OverdrawsTheLastGrant_ForWorkNoGrantHadRoomFor()
    {
        // Overdraft-once. The provider has been paid, so refusing now means eating the cost and
        // giving the customer nothing. The last grant goes negative, which is what makes the overrun
        // visible rather than silently absent.
        HaveGrants(Grant(GrantId1, 10));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 1));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Service().SettleAsync(Settle(500));

        Assert.True(result.Overdrawn);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 500 });
    }

    [Fact]
    public async Task SettleAsync_ChargesTheReservedGrant_ForAnAccountWhoseGrantsAllExpired()
    {
        // Nothing is allocatable any more, so there is no last allocation to overdraw. The grant the
        // work was actually held against takes the charge -- anything else would invent one.
        HaveGrants();
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Service().SettleAsync(Settle(7));

        Assert.True(result.Overdrawn);
        VerifySettled(new Dictionary<Guid, long> { [GrantId1] = 7 });
    }

    [Theory]
    [InlineData(20, 0.8)] // 80 of 100 left
    [InlineData(95, 0.05)] // 5 of 100 left: the warning zone
    [InlineData(500, 0.0)] // overdrawn counts as empty, not negative
    public async Task SettleAsync_ReportsWhatIsLeft_ForTheAccountsLiveGrants(
        long charged,
        double expected
    )
    {
        HaveGrants(Grant(GrantId1, 100));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 1));
        HaveOutstanding(Outstanding(GrantId1, 1));

        var result = await Service().SettleAsync(Settle(charged));

        Assert.Equal(expected, result.RemainingRatio!.Value, precision: 6);
    }

    [Fact]
    public async Task SettleAsync_WritesNothing_ForAReplayWithNoOutstandingReservations()
    {
        HaveOutstanding();

        var result = await Service().SettleAsync(Settle(20));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        VerifySettled(new Dictionary<Guid, long>());
    }

    [Fact]
    public async Task ReleaseAsync_GivesTheHoldBack_ForAFailedStep()
    {
        var result = await Service()
            .ReleaseAsync(new ReleaseQuotaRequest(AccountId, TestUsage.Extraction, TestUsage.Page));

        Assert.Equal(SettleQuotaResultCode.Success, result.ResultCode);
        _consumptionWriter.Verify(
            w =>
                w.ReleaseAsync(
                    AccountId,
                    TestUsage.Extraction,
                    TestUsage.Page,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ReserveAsync_ReportsUnauthorized_ForAGrantReaderThatRefuses()
    {
        _grants
            .Setup(r =>
                r.GetActiveGrantsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new GetActiveGrantsResult(GetActiveGrantsResultCode.Unauthorized, null));

        var result = await Service().ReserveAsync(Reserve(1));

        Assert.Equal(ReserveQuotaResultCode.Unauthorized, result.ResultCode);
    }

    private void VerifySettled(IReadOnlyDictionary<Guid, long> expected) =>
        _consumptionWriter.Verify(
            w =>
                w.SettleAsync(
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

    [Theory]
    [InlineData(
        GetOutstandingReservationsResultCode.Unauthorized,
        SettleQuotaResultCode.Unauthorized
    )]
    [InlineData(GetOutstandingReservationsResultCode.Failed, SettleQuotaResultCode.Failed)]
    public async Task SettleAsync_SettlesNothing_ForAnOutstandingReadThatDidNotSucceed(
        GetOutstandingReservationsResultCode readResult,
        SettleQuotaResultCode expected
    )
    {
        HaveGrants(Grant(GrantId1, 100));
        _consumptionReader
            .Setup(r =>
                r.GetOutstandingReservationsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new GetOutstandingReservationsResult(readResult, "no"));

        var result = await Service().SettleAsync(Settle(20));

        Assert.Equal(expected, result.ResultCode);
        _consumptionWriter.Verify(
            w =>
                w.SettleAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<Guid, long>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    private static SettleQuotaRequest Settle(long actual) =>
        new(AccountId, TestUsage.Extraction, TestUsage.Page, actual);

    private void HaveGrants(params Grant[] grants) =>
        _grants
            .Setup(r =>
                r.GetActiveGrantsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new GetActiveGrantsResult(GetActiveGrantsResultCode.Success, null, [.. grants])
            );

    private void HaveTotals(params GrantTotalConsumptions[] totals) =>
        _consumptionReader
            .Setup(r =>
                r.GetGrantConsumptionTotalsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new GetGrantConsumptionTotalsResult(
                    GetGrantConsumptionTotalsResultCode.Success,
                    null,
                    [.. totals]
                )
            );

    private void HaveOutstanding(params ConsumptionEvent[] outstanding) =>
        _consumptionReader
            .Setup(r =>
                r.GetOutstandingReservationsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new GetOutstandingReservationsResult(
                    GetOutstandingReservationsResultCode.Success,
                    null,
                    [.. outstanding]
                )
            );

    private Grant Grant(Guid grantId, long quantity, int expiresInDays = 30)
    {
        var result = Billing.Grants.Models.Grant.Create(
            accountId: AccountId,
            quantity: quantity,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            validFromUtc: _time.GetUtcNow().UtcDateTime.AddDays(-1),
            validToUtc: _time.GetUtcNow().UtcDateTime.AddDays(expiresInDays),
            grantId: grantId
        );
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private ConsumptionEvent Outstanding(Guid grantId, long quantity)
    {
        var result = ConsumptionEvent.Create(
            accountId: AccountId,
            quantity: quantity,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            provider: "prov",
            utcTimestamp: _time.GetUtcNow().UtcDateTime,
            grantId: grantId
        );
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsAvailable_ForAGrantWithRoom()
    {
        HaveGrants(Grant(GrantId1, 100));

        Assert.Equal(QuotaAvailability.Available, await Availability());
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsExhausted_ForAnAccountWithNoGrants()
    {
        HaveGrants();

        Assert.Equal(QuotaAvailability.Exhausted, await Availability());
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsExhausted_ForGrantsWithNothingLeft()
    {
        // The case worth catching in the initializer: grants exist, so a naive "any grants?" check
        // would let a document through that could never be processed.
        HaveGrants(Grant(GrantId1, 10));
        HaveTotals(new GrantTotalConsumptions(GrantId1, 10));

        Assert.Equal(QuotaAvailability.Exhausted, await Availability());
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsUnknown_ForAGrantStoreThatThrows()
    {
        // The initializer refuses only on Exhausted. A database blip must not stop every document in
        // the system from starting, to save the pipeline from a rejection a step would have reached
        // seconds later anyway.
        _grants
            .Setup(r =>
                r.GetActiveGrantsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("database is down"));

        Assert.Equal(QuotaAvailability.Unknown, await Availability());
    }

    [Fact]
    public async Task GetAvailabilityAsync_WritesNothing_ForAnyOutcome()
    {
        // It is a question, not a hold. Reserving here would leave a row behind for every document
        // that was merely being considered.
        HaveGrants(Grant(GrantId1, 100));

        await Availability();

        _consumptionWriter.Verify(
            w => w.ReserveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private Task<QuotaAvailability> Availability() =>
        Service().GetAvailabilityAsync(AccountId, TestUsage.Extraction, TestUsage.Page);
}
