using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;

namespace Corely.Billing.Consumption.Models;

public sealed record ConsumptionEvent
{
    internal const int QUANTITY_MIN_VALUE = 0;
    internal const int PROVIDER_MAX_LENGTH = 100;
    internal const int IDEMPOTENCY_KEY_MAX_LENGTH = 400;

    public Guid ConsumptionId { get; }
    public Guid AccountId { get; }
    public long Quantity { get; }
    public UsageUnit Unit { get; }
    public UsageOperation Operation { get; }
    public string Provider { get; }
    public DateTime UtcTimestamp { get; }

    /// <summary>Joins this row to the logs of the work that caused it. Stamped by the writer.</summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// Identifies the billable work this row records, the same across every retry of it. Stamped by
    /// the writer; a unique index on <c>(AccountId, IdempotencyKey)</c> is what makes a retry a
    /// no-op instead of a second charge.
    /// </summary>
    public string IdempotencyKey { get; } = string.Empty;

    public Guid GrantId { get; }

    /// <summary>When the reservation this row represents resolved; null while it is outstanding.</summary>
    public DateTime? FinalizedUtc { get; }

    /// <summary>How it resolved; null while it is outstanding.</summary>
    public ConsumptionOutcome? Outcome { get; }

    public Guid? UserId { get; }
    public IReadOnlyDictionary<string, string>? Tags { get; }

    private ConsumptionEvent(
        Guid consumptionId,
        Guid accountId,
        long quantity,
        UsageUnit unit,
        UsageOperation operation,
        string provider,
        DateTime utcTimestamp,
        Guid correlationId,
        string idempotencyKey,
        Guid grantId,
        DateTime? finalizedUtc = null,
        ConsumptionOutcome? outcome = null,
        Guid? userId = null,
        IReadOnlyDictionary<string, string>? tags = null
    )
    {
        ConsumptionId = consumptionId;
        AccountId = accountId;
        Quantity = quantity;
        Unit = unit;
        Operation = operation;
        Provider = provider;
        UtcTimestamp = utcTimestamp;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        GrantId = grantId;
        FinalizedUtc = finalizedUtc;
        Outcome = outcome;
        UserId = userId;
        Tags = tags;
    }

    /// <remarks>
    /// Takes neither a correlation id nor an idempotency key. Both are properties of the ambient
    /// operation rather than of the caller's request, and a caller free to invent them is a caller
    /// free to invent a fresh one per retry -- which is exactly the double-charge this design
    /// removes. <see cref="Services.ConsumptionWriter"/> stamps them.
    /// </remarks>
    public static CreateResult<ConsumptionEvent> Create(
        Guid accountId,
        long quantity,
        UsageUnit unit,
        UsageOperation operation,
        string provider,
        DateTime utcTimestamp,
        Guid grantId,
        Guid? consumptionId = null,
        Guid? userId = null,
        IReadOnlyDictionary<string, string>? tags = null
    )
    {
        var candidate = new ConsumptionEvent(
            consumptionId ?? Guid.CreateVersion7(),
            accountId,
            quantity,
            unit,
            operation,
            provider,
            utcTimestamp,
            Guid.Empty,
            string.Empty,
            grantId,
            finalizedUtc: null,
            outcome: null,
            userId,
            tags
        );

        return Invalidity(candidate) is { } invalidity
            ? CreateResult<ConsumptionEvent>.Invalid(invalidity)
            : CreateResult<ConsumptionEvent>.Success(candidate);
    }

    private static string? Invalidity(ConsumptionEvent candidate)
    {
        if (candidate.AccountId == Guid.Empty)
            return $"{nameof(AccountId)} is required.";

        if (candidate.GrantId == Guid.Empty)
            return $"{nameof(GrantId)} is required.";

        if (candidate.Quantity < QUANTITY_MIN_VALUE)
            return $"{nameof(Quantity)} must be {QUANTITY_MIN_VALUE} or greater.";

        if (string.IsNullOrWhiteSpace(candidate.Provider))
            return $"{nameof(Provider)} is required.";

        if (candidate.Provider.Length > PROVIDER_MAX_LENGTH)
            return $"{nameof(Provider)} is at most {PROVIDER_MAX_LENGTH} characters.";

        return null;
    }

    /// <summary>
    /// Applies everything the writer owns -- the ambient operation's identity, and whether the row
    /// is already resolved -- immediately before persisting.
    /// </summary>
    /// <remarks>
    /// A reservation is written with <paramref name="finalizedUtc"/> and <paramref name="outcome"/>
    /// null; a settled charge with both set. That is the entire difference between the two, which is
    /// why a reservation is a row in this table rather than a table of its own: one query surface,
    /// one place to look for holds nobody ever resolved.
    /// </remarks>
    internal ConsumptionEvent Stamp(
        Guid correlationId,
        string idempotencyKey,
        DateTime? finalizedUtc,
        ConsumptionOutcome? outcome
    )
    {
        ArgumentOutOfRangeException.ThrowIfEqual(correlationId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (finalizedUtc.HasValue != outcome.HasValue)
        {
            throw new ArgumentException(
                "A consumption row is either outstanding or resolved. FinalizedUtc and Outcome are "
                    + "set together or not at all.",
                nameof(outcome)
            );
        }

        return new(
            ConsumptionId,
            AccountId,
            Quantity,
            Unit,
            Operation,
            Provider,
            UtcTimestamp,
            correlationId,
            idempotencyKey,
            GrantId,
            finalizedUtc,
            outcome,
            UserId,
            Tags
        );
    }

    internal static ConsumptionEvent FromEntity(ConsumptionEventEntity entity) =>
        new(
            entity.ConsumptionId,
            entity.AccountId,
            entity.Quantity,
            entity.Unit,
            entity.Operation,
            entity.Provider,
            entity.UtcTimestamp,
            entity.CorrelationId,
            entity.IdempotencyKey,
            entity.GrantId,
            entity.FinalizedUtc,
            entity.Outcome,
            entity.UserId,
            TagSerializer.Deserialize(entity.TagsJson)
        );
}
