using System.Linq.Expressions;
using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Operations;
using Corely.Billing.Validators;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Consumption.Processors;

public class ConsumptionProcessorTests
{
    private static readonly Guid GrantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherGrantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid AccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CorrelationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ServiceFactory _serviceFactory = new();
    private readonly Mock<ILogger<ConsumptionProcessor>> _logger = new();

    private ConsumptionProcessor CreateProcessor(
        IRepo<ConsumptionEventEntity> repo,
        IOperationContextAccessor? accessor = null
    ) =>
        new(
            repo,
            accessor ?? Accessor("scope-a"),
            _serviceFactory.GetRequiredService<IValidationProvider>(),
            _serviceFactory.TimeProvider,
            _logger.Object
        );

    private IRepo<ConsumptionEventEntity> MockRepo() =>
        _serviceFactory.GetRequiredService<IRepo<ConsumptionEventEntity>>();

    [Fact]
    public async Task ReserveAsync_PersistsAnOutstandingRow_ForAnEventUnderAnOperationScope()
    {
        var repo = MockRepo();
        var consumptionEvent = MakeEvent(3);

        var result = await CreateProcessor(repo).ReserveAsync(consumptionEvent);

        Assert.Equal(ReserveConsumptionResultCode.Success, result.ResultCode);
        var entity = Assert.Single(await repo.ListAsync(_ => true));
        Assert.Equal(AccountId, entity.AccountId);
        Assert.Equal(3, entity.Quantity);
        Assert.Equal(TestUsage.Page, entity.Unit);
        Assert.Equal(TestUsage.Extraction, entity.Operation);
        Assert.Equal("prov", entity.Provider);
        Assert.Equal(GrantId, entity.GrantId);
        Assert.Null(entity.FinalizedUtc);
        Assert.Null(entity.Outcome);
        Assert.Equal(7, entity.ConsumptionId.Version);
    }

    [Fact]
    public async Task ReserveAsync_StampsTheAmbientIdentity_ForAnEventThatCarriesNone()
    {
        // A caller free to supply the correlation id or key is free to supply a different one per
        // attempt, which is the double-charge.
        var repo = MockRepo();

        await CreateProcessor(repo, Accessor("job:abc/step:def")).ReserveAsync(MakeEvent(3));

        var entity = Assert.Single(await repo.ListAsync(_ => true));
        Assert.Equal(CorrelationId, entity.CorrelationId);
        Assert.Equal(
            $"job:abc/step:def|{TestUsage.Extraction}|{TestUsage.Page}|{GrantId:N}",
            entity.IdempotencyKey
        );
    }

    [Fact]
    public async Task ReserveAsync_RecordsNothing_ForNoAmbientOperationContext()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        var accessor = new Mock<IOperationContextAccessor>();
        accessor.SetupGet(a => a.Current).Returns((OperationContext?)null);

        var result = await CreateProcessor(repo.Object, accessor.Object).ReserveAsync(MakeEvent(1));

        Assert.Equal(ReserveConsumptionResultCode.NotRecordedError, result.ResultCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReserveAsync_RecordsNothing_ForAnUnregisteredOperation()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        var consumptionEvent = MakeEvent(1);
        consumptionEvent.Operation = TestUsage.Unregistered;

        var result = await CreateProcessor(repo.Object).ReserveAsync(consumptionEvent);

        Assert.Equal(ReserveConsumptionResultCode.ValidationError, result.ResultCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReserveAsync_Succeeds_ForAConcurrentWriteThatWonTheKey()
    {
        // Both attempts find nothing, and the loser's insert fails on the unique index. That row is
        // the winner's, so the work is recorded.
        var repo = MockRepoThatFails(new DbUpdateException("duplicate key"));
        SetupGet(repo)
            .ReturnsAsync((ConsumptionEventEntity?)null)
            .ReturnsAsync(new ConsumptionEventEntity());

        var result = await CreateProcessor(repo.Object).ReserveAsync(MakeEvent(1));

        Assert.Equal(ReserveConsumptionResultCode.Success, result.ResultCode);
        Assert.Equal("Already recorded.", result.Message);
    }

    [Fact]
    public async Task ReserveAsync_Fails_ForADuplicateKeyWhoseRowIsAbsent()
    {
        // Same exception type, opposite meaning: reporting success would report revenue no row backs.
        var repo = MockRepoThatFails(new DbUpdateException("something else"));
        SetupExistingRow(repo, null);

        var result = await CreateProcessor(repo.Object).ReserveAsync(MakeEvent(1));

        Assert.Equal(ReserveConsumptionResultCode.NotRecordedError, result.ResultCode);
    }

    [Fact]
    public async Task ReserveAsync_Retries_ForATransientFailureThatThenSucceeds()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        repo.SetupSequence(r =>
                r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("boom"))
            .ReturnsAsync(new ConsumptionEventEntity());
        SetupExistingRow(repo, null);

        var result = await CreateProcessor(repo.Object).ReserveAsync(MakeEvent(1));

        Assert.Equal(ReserveConsumptionResultCode.Success, result.ResultCode);
        repo.Verify(
            r => r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        VerifyLogged(LogLevel.Warning, "Retrying consumption write attempt", Times.AtLeastOnce());
    }

    [Fact]
    public async Task ReserveAsync_Fails_ForCancellation()
    {
        var repo = MockRepoThatFails(new OperationCanceledException());
        SetupExistingRow(repo, null);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await CreateProcessor(repo.Object).ReserveAsync(MakeEvent(1), cts.Token);

        Assert.Equal(ReserveConsumptionResultCode.NotRecordedError, result.ResultCode);
        VerifyLogged(LogLevel.Error, "Failed to reserve consumption for account", Times.Once());
    }

    [Fact]
    public async Task ReserveAsync_Fails_ForExhaustedRetries()
    {
        // The provider has already been called; a success here reports income with no row behind it.
        var repo = MockRepoThatFails(new InvalidOperationException("boom"));
        SetupExistingRow(repo, null);

        var result = await CreateProcessor(repo.Object).ReserveAsync(MakeEvent(1));

        Assert.Equal(ReserveConsumptionResultCode.NotRecordedError, result.ResultCode);
        repo.Verify(
            r => r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
        VerifyLogged(LogLevel.Error, "Failed to reserve consumption for account", Times.Once());
    }

    [Fact]
    public async Task ReserveAsync_HoldsTheReleasedRowAgain_ForAStepRetriedAfterItsHoldWasReleased()
    {
        // The key is unique, so holding again has to reuse the row rather than insert a second one.
        var released = new ConsumptionEventEntity
        {
            Quantity = 5,
            FinalizedUtc = Now.AddMinutes(-1),
            Outcome = ConsumptionOutcome.Released,
        };
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        SetupExistingRow(repo, released);
        repo.Setup(r =>
                r.UpdateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        var result = await CreateProcessor(repo.Object).ReserveAsync(MakeEvent(1));

        Assert.Equal(ReserveConsumptionResultCode.Success, result.ResultCode);
        Assert.Null(released.FinalizedUtc);
        Assert.Null(released.Outcome);
        Assert.Equal(1, released.Quantity);
        repo.Verify(r => r.UpdateAsync(released, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveAsync_LeavesTheRowAlone_ForAReplayOfASettledStep()
    {
        var settled = new ConsumptionEventEntity
        {
            Quantity = 40,
            FinalizedUtc = Now.AddMinutes(-1),
            Outcome = ConsumptionOutcome.Settled,
        };
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        SetupExistingRow(repo, settled);

        var result = await CreateProcessor(repo.Object).ReserveAsync(MakeEvent(1));

        Assert.Equal(ReserveConsumptionResultCode.Success, result.ResultCode);
        Assert.Equal("Already recorded.", result.Message);
        Assert.Equal(40, settled.Quantity);
        Assert.Equal(ConsumptionOutcome.Settled, settled.Outcome);
    }

    [Fact]
    public async Task SettleAsync_SettlesTheHoldAtTheActualQuantity_ForAReservedGrant()
    {
        var repo = MockRepo();
        var processor = CreateProcessor(repo);
        await processor.ReserveAsync(MakeEvent(10));

        var result = await processor.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 7 }
        );

        Assert.Equal(ResolveConsumptionResultCode.Success, result.ResultCode);
        Assert.Equal(7, result.SettledQuantity);
        Assert.Equal(1, result.RowCount);
        var entity = Assert.Single(await repo.ListAsync(_ => true));
        Assert.Equal(7, entity.Quantity);
        Assert.Equal(ConsumptionOutcome.Settled, entity.Outcome);
        Assert.Equal(Now, entity.FinalizedUtc);
    }

    [Fact]
    public async Task SettleAsync_WritesASpilloverRow_ForAGrantThatWasNeverReserved()
    {
        var repo = MockRepo();
        var processor = CreateProcessor(repo);
        await processor.ReserveAsync(MakeEvent(10));

        var result = await processor.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 10, [OtherGrantId] = 4 }
        );

        Assert.Equal(14, result.SettledQuantity);
        Assert.Equal(2, result.RowCount);
        var spillover = Assert.Single(await repo.ListAsync(e => e.GrantId == OtherGrantId));
        Assert.Equal(4, spillover.Quantity);
        Assert.Equal(ConsumptionOutcome.Settled, spillover.Outcome);
        Assert.EndsWith($"{OtherGrantId:N}", spillover.IdempotencyKey);
    }

    [Fact]
    public async Task SettleAsync_ReleasesTheHold_ForAReservedGrantMissingFromTheSplit()
    {
        var repo = MockRepo();
        var processor = CreateProcessor(repo);
        await processor.ReserveAsync(MakeEvent(10));

        await processor.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [OtherGrantId] = 3 }
        );

        var held = Assert.Single(await repo.ListAsync(e => e.GrantId == GrantId));
        Assert.Equal(ConsumptionOutcome.Released, held.Outcome);
        Assert.Equal(10, held.Quantity);
    }

    [Fact]
    public async Task SettleAsync_WritesNothing_ForAReplayWithNothingOutstanding()
    {
        var repo = MockRepo();
        var processor = CreateProcessor(repo);
        await processor.ReserveAsync(MakeEvent(10));
        var split = new Dictionary<Guid, long> { [GrantId] = 10, [OtherGrantId] = 4 };
        await processor.SettleAsync(AccountId, TestUsage.Extraction, TestUsage.Page, split);

        var replay = await processor.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            split
        );

        Assert.Equal(ResolveConsumptionResultCode.Success, replay.ResultCode);
        Assert.Equal(0, replay.RowCount);
        Assert.Equal(2, await repo.CountAsync());
    }

    [Fact]
    public async Task ReleaseAsync_ReleasesTheHoldKeepingItsQuantity_ForAReservedGrant()
    {
        var repo = MockRepo();
        var processor = CreateProcessor(repo);
        await processor.ReserveAsync(MakeEvent(10));

        var result = await processor.ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        Assert.Equal(ResolveConsumptionResultCode.Success, result.ResultCode);
        Assert.Equal(0, result.SettledQuantity);
        var entity = Assert.Single(await repo.ListAsync(_ => true));
        Assert.Equal(ConsumptionOutcome.Released, entity.Outcome);
        Assert.Equal(10, entity.Quantity);
    }

    [Fact]
    public async Task ReleaseAsync_LeavesOtherScopesAlone_ForAHoldUnderADifferentScope()
    {
        var repo = MockRepo();
        await CreateProcessor(repo, Accessor("scope-b")).ReserveAsync(MakeEvent(10));

        await CreateProcessor(repo).ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        var entity = Assert.Single(await repo.ListAsync(_ => true));
        Assert.Null(entity.Outcome);
    }

    [Fact]
    public async Task SettleAsync_RecordsNothing_ForNoAmbientOperationContext()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        var accessor = new Mock<IOperationContextAccessor>();

        var result = await CreateProcessor(repo.Object, accessor.Object)
            .SettleAsync(
                AccountId,
                TestUsage.Extraction,
                TestUsage.Page,
                new Dictionary<Guid, long> { [GrantId] = 1 }
            );

        Assert.Equal(ResolveConsumptionResultCode.NotRecordedError, result.ResultCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListOutstandingReservationsAsync_ReturnsNull_ForNoAmbientOperationContext()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        var accessor = new Mock<IOperationContextAccessor>();

        var outstanding = await CreateProcessor(repo.Object, accessor.Object)
            .ListOutstandingReservationsAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        Assert.Null(outstanding);
    }

    private static Mock<IRepo<ConsumptionEventEntity>> MockRepoThatFails(Exception ex)
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        repo.Setup(r =>
                r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(ex);
        return repo;
    }

    private static Moq.Language.ISetupSequentialResult<Task<ConsumptionEventEntity?>> SetupGet(
        Mock<IRepo<ConsumptionEventEntity>> repo
    ) =>
        repo.SetupSequence(r =>
            r.GetAsync(
                It.IsAny<Expression<Func<ConsumptionEventEntity, bool>>>(),
                It.IsAny<Func<
                    IQueryable<ConsumptionEventEntity>,
                    IOrderedQueryable<ConsumptionEventEntity>
                >?>(),
                It.IsAny<Func<
                    IQueryable<ConsumptionEventEntity>,
                    IQueryable<ConsumptionEventEntity>
                >?>(),
                It.IsAny<CancellationToken>()
            )
        );

    private static void SetupExistingRow(
        Mock<IRepo<ConsumptionEventEntity>> repo,
        ConsumptionEventEntity? existing
    ) =>
        repo.Setup(r =>
                r.GetAsync(
                    It.IsAny<Expression<Func<ConsumptionEventEntity, bool>>>(),
                    It.IsAny<Func<
                        IQueryable<ConsumptionEventEntity>,
                        IOrderedQueryable<ConsumptionEventEntity>
                    >?>(),
                    It.IsAny<Func<
                        IQueryable<ConsumptionEventEntity>,
                        IQueryable<ConsumptionEventEntity>
                    >?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existing);

    private void VerifyLogged(LogLevel level, string fragment, Times times) =>
        _logger.Verify(
            l =>
                l.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(fragment)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times
        );

    private static IOperationContextAccessor Accessor(string idempotencyScope)
    {
        var accessor = new Mock<IOperationContextAccessor>();
        accessor
            .SetupGet(a => a.Current)
            .Returns(new OperationContext(CorrelationId, idempotencyScope));
        return accessor.Object;
    }

    private static ConsumptionEvent MakeEvent(long quantity) =>
        new()
        {
            AccountId = AccountId,
            GrantId = GrantId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = quantity,
            Provider = "prov",
            UtcTimestamp = Now,
        };
}
