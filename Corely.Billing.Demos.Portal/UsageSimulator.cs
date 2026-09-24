using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;

namespace Corely.Billing.Demos.Portal;

internal sealed class UsageSimulator(IQuotaService quotaService, IOperationContextAccessor accessor)
{
    public async Task<string> RunAsync(UsageOperation operation, long pages)
    {
        using var scope = accessor.BeginScope(
            new OperationContext(Guid.CreateVersion7(), $"simulation:{Guid.CreateVersion7()}")
        );

        var reserved = await quotaService.ReserveAsync(
            new ReserveQuotaRequest(
                DemoUsage.AccountId,
                operation,
                DemoUsage.Page,
                1,
                DemoUsage.PROVIDER
            )
        );
        if (reserved.ResultCode != ReserveQuotaResultCode.Success)
            return $"Refused: {reserved.Message}";

        var settled = await quotaService.SettleAsync(
            new SettleQuotaRequest(DemoUsage.AccountId, operation, DemoUsage.Page, pages)
        );
        return settled.Overdrawn
            ? $"Used {pages:N0} pages and overdrew the last grant."
            : $"Used {pages:N0} pages.";
    }
}
