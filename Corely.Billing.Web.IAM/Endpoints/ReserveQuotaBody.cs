using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.IAM.Endpoints;

internal sealed record ReserveQuotaBody(
    Guid OperationId,
    string Unit,
    long Quantity,
    string Provider,
    Dictionary<string, string>? Tags = null
)
{
    public const string IDEMPOTENCY_SCOPE_PREFIX = "api:";

    public OperationContext OperationContext =>
        new(OperationId, $"{IDEMPOTENCY_SCOPE_PREFIX}{OperationId}");

    public ReserveQuotaRequest? ToReserveQuotaRequest(
        Guid accountId,
        UsageOperation operation,
        Guid? userId
    ) =>
        UsageUnit.TryFrom(Unit, out var unit)
            ? new(accountId, operation, unit, Quantity, Provider, userId, Tags)
            : null;
}
