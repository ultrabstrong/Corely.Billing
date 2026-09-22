namespace Corely.Billing;

/// <summary>
/// Identifies the unit of work an operation belongs to.
/// </summary>
/// <param name="CorrelationId">Joins everything the operation did to the logs that describe it.</param>
/// <param name="IdempotencyScope">
/// Names the unit of billable work. Must be <em>attempt-invariant</em> — identical across every retry
/// of the same work — and unique per unit of work. That is what lets a retried step recognise its own
/// earlier write instead of charging twice.
/// </param>
/// <remarks>
/// Ambient by design. A caller asking for a document to be extracted has no business knowing that
/// metering needs an idempotency key, so nothing is passed down; services that need it read it from
/// <see cref="IOperationContextAccessor"/>.
/// </remarks>
public sealed record OperationContext(Guid CorrelationId, string IdempotencyScope);
