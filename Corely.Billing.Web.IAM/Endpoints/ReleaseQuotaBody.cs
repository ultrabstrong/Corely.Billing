using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.IAM.Endpoints;

internal sealed record ReleaseQuotaBody(Guid OperationId, string Unit)
{
    public OperationContext OperationContext =>
        new(OperationId, $"{ReserveQuotaBody.IDEMPOTENCY_SCOPE_PREFIX}{OperationId}");

    public ReleaseQuotaRequest? ToReleaseQuotaRequest(Guid accountId, UsageOperation operation) =>
        UsageUnit.TryFrom(Unit, out var unit) ? new(accountId, operation, unit) : null;
}
