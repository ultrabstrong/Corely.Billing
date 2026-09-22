using System.Linq.Expressions;
using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.UnitTests.Consumption.Services;

public class ConsumptionWriterTests
{
    private static readonly Guid TestGrantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TestCorrelationId = Guid.Parse(
        "33333333-3333-3333-3333-333333333333"
    );

    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<ILogger<ConsumptionWriter>> _logger = new();

    [Fact]
    public async Task SaveAsync_Persists_ForAnEventUnderAnOperationScope()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<ConsumptionEventEntity>>();
        var writer = new ConsumptionWriter(
            repo,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );
        var evt = MakeEvent(3, TestUsage.Extraction);

        await writer.SaveAsync(evt);

        var entity = await repo.GetAsync(e => e.ConsumptionId == evt.ConsumptionId);
        Assert.NotNull(entity);
        Assert.Multiple(() =>
        {
            Assert.Equal(evt.AccountId, entity!.AccountId);
            Assert.Equal(evt.Quantity, entity.Quantity);
            Assert.Equal(evt.Unit, entity.Unit);
            Assert.Equal(evt.Operation, entity.Operation);
            Assert.Equal(evt.Provider, entity.Provider);
            Assert.Equal(evt.UtcTimestamp, entity.UtcTimestamp);
            Assert.Equal(evt.GrantId, entity.GrantId);
        });
    }

    [Fact]
    public async Task SaveAsync_StampsTheAmbientIdentity_ForAnEventThatCarriesNone()
    {
        // The reason correlation and idempotency came off ConsumptionEvent.Create: a caller free to
        // supply them is a caller free to supply a different one per attempt, which is the
        // double-charge.
        var repo = ServiceFixture.GetRequiredService<IRepo<ConsumptionEventEntity>>();
        var writer = new ConsumptionWriter(
            repo,
            Accessor("job:abc/step:def"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );
        var evt = MakeEvent(3, TestUsage.Extraction);

        Assert.Equal(Guid.Empty, evt.CorrelationId);
        Assert.Empty(evt.IdempotencyKey);

        await writer.SaveAsync(evt);

        var entity = await repo.GetAsync(e => e.ConsumptionId == evt.ConsumptionId);
        Assert.Equal(TestCorrelationId, entity!.CorrelationId);
        Assert.Equal(
            $"job:abc/step:def|{TestUsage.Extraction}|{TestUsage.Page}|{TestGrantId:N}",
            entity.IdempotencyKey
        );
    }

    [Fact]
    public async Task SaveAsync_Fails_ForNoAmbientOperationContext()
    {
        // Deliberately not "generate one and carry on". A made-up identity is exactly what let every
        // retry charge again, so its absence has to be loud rather than papered over.
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        var accessor = new Mock<IOperationContextAccessor>();
        accessor.SetupGet(a => a.Current).Returns((OperationContext?)null);

        var writer = new ConsumptionWriter(
            repo.Object,
            accessor.Object,
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.SaveAsync(MakeEvent(1, TestUsage.NoOp));

        Assert.Equal(SaveConsumptionResultCode.Failed, result.ResultCode);
        repo.Verify(
            r => r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SaveAsync_Succeeds_ForAConcurrentWriteThatWonTheKey()
    {
        // Two attempts writing the same key at once: the lookup finds nothing for either, and the
        // loser's insert fails on the unique index. That row is the winner's, so it's recorded.
        var repo = MockRepoThatFails(new DbUpdateException("duplicate key"));
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
            )
            .ReturnsAsync((ConsumptionEventEntity?)null)
            .ReturnsAsync(new ConsumptionEventEntity());

        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.SaveAsync(MakeEvent(1, TestUsage.NoOp));

        Assert.Multiple(() =>
        {
            Assert.Equal(SaveConsumptionResultCode.Success, result.ResultCode);
            Assert.Equal("Already recorded.", result.Message);
        });
    }

    [Fact]
    public async Task SaveAsync_Fails_ForADuplicateKeyWhoseRowIsAbsent()
    {
        // Same exception type, opposite meaning: something else went wrong and nothing was
        // recorded. Reporting success here would report revenue that no row backs.
        var repo = MockRepoThatFails(new DbUpdateException("something else"));
        SetupExistingRow(repo, null);

        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.SaveAsync(MakeEvent(1, TestUsage.NoOp));

        Assert.Equal(SaveConsumptionResultCode.Failed, result.ResultCode);
    }

    [Fact]
    public async Task SaveAsync_Retries_ForATransientFailureThatThenSucceeds()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        repo.SetupSequence(r =>
                r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("boom"))
            .ReturnsAsync(new ConsumptionEventEntity());
        SetupExistingRow(repo, null);

        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.SaveAsync(MakeEvent(1, TestUsage.NoOp));

        Assert.Equal(SaveConsumptionResultCode.Success, result.ResultCode);
        repo.Verify(
            r => r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        VerifyLogged(LogLevel.Warning, "Retrying consumption write attempt", Times.AtLeastOnce());
    }

    [Fact]
    public async Task SaveAsync_Fails_ForCancellation()
    {
        var repo = MockRepoThatFails(new OperationCanceledException());

        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await writer.SaveAsync(MakeEvent(1, TestUsage.NoOp), cts.Token);

        Assert.Equal(SaveConsumptionResultCode.Failed, result.ResultCode);
        VerifyLogged(LogLevel.Error, "Failed to save consumption for account", Times.Once());
    }

    [Fact]
    public async Task SaveAsync_Fails_ForExhaustedRetries()
    {
        // Was a warning plus a success code. The provider had already been called and billed us, so
        // that combination reported income the ledger has no row for.
        var repo = MockRepoThatFails(new InvalidOperationException("boom"));
        SetupExistingRow(repo, null);

        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.SaveAsync(MakeEvent(1, TestUsage.NoOp));

        Assert.Equal(SaveConsumptionResultCode.Failed, result.ResultCode);
        repo.Verify(
            r => r.CreateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
        VerifyLogged(LogLevel.Error, "Failed to save consumption for account", Times.Once());
    }

    [Fact]
    public async Task ReserveAsync_HoldsTheReleasedRowAgain_ForAStepRetriedAfterItsHoldWasReleased()
    {
        // A step that failed after reserving gave its hold back. Run again under the same scope, it
        // must hold again, on the same row, since the key is unique. Before, it tried to insert a
        // second row, hit the index, and the step failed as "consumption not recorded".
        var released = new ConsumptionEventEntity
        {
            Quantity = 5,
            FinalizedUtc = Now.UtcDateTime.AddMinutes(-1),
            Outcome = ConsumptionOutcome.Released,
        };
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        SetupExistingRow(repo, released);
        repo.Setup(r =>
                r.UpdateAsync(It.IsAny<ConsumptionEventEntity>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.ReserveAsync(MakeEvent(1, TestUsage.Extraction));

        Assert.Multiple(() =>
        {
            Assert.Equal(SaveConsumptionResultCode.Success, result.ResultCode);
            Assert.Null(released.FinalizedUtc);
            Assert.Null(released.Outcome);
            Assert.Equal(1, released.Quantity);
        });
        repo.Verify(r => r.UpdateAsync(released, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveAsync_LeavesTheRowAlone_ForAReplayOfASettledStep()
    {
        // Already charged: the replay is recognised and nothing is written, so it can't charge again.
        var settled = new ConsumptionEventEntity
        {
            Quantity = 40,
            FinalizedUtc = Now.UtcDateTime.AddMinutes(-1),
            Outcome = ConsumptionOutcome.Settled,
        };
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        SetupExistingRow(repo, settled);

        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.ReserveAsync(MakeEvent(1, TestUsage.Extraction));

        Assert.Multiple(() =>
        {
            Assert.Equal(SaveConsumptionResultCode.Success, result.ResultCode);
            Assert.Equal("Already recorded.", result.Message);
            Assert.Equal(40, settled.Quantity);
            Assert.Equal(ConsumptionOutcome.Settled, settled.Outcome);
        });
    }

    [Fact]
    public async Task SaveAsync_RecordsNothing_ForAnUnregisteredOperation()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.SaveAsync(MakeEvent(1, UsageOperation.From("never_registered")));

        Assert.Equal(SaveConsumptionResultCode.UnknownUsage, result.ResultCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReserveAsync_RecordsNothing_ForAnUnregisteredOperation()
    {
        var repo = new Mock<IRepo<ConsumptionEventEntity>>(MockBehavior.Strict);
        var writer = new ConsumptionWriter(
            repo.Object,
            Accessor("scope-a"),
            TestUsage.Vocabulary,
            _time,
            _logger.Object
        );

        var result = await writer.ReserveAsync(
            MakeEvent(1, UsageOperation.From("never_registered"))
        );

        Assert.Equal(SaveConsumptionResultCode.UnknownUsage, result.ResultCode);
        repo.VerifyNoOtherCalls();
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
            .Returns(new OperationContext(TestCorrelationId, idempotencyScope));
        return accessor.Object;
    }

    private static ConsumptionEvent MakeEvent(long quantity, UsageOperation operation)
    {
        var createResult = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: quantity,
            unit: TestUsage.Page,
            operation: operation,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );
        Assert.True(createResult.IsSuccess);
        return createResult.Value;
    }
}
