namespace Corely.Billing.Operations;

public sealed record OperationContext(Guid CorrelationId, string IdempotencyScope);
