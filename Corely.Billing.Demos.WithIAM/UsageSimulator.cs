using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;

namespace Corely.Billing.Demos.WithIAM;

internal sealed class UsageSimulator(IQuotaService quotaService, IOperationContextAccessor accessor)
{
    public async Task<string> RunAsync(Guid accountId, long pages)
    {
        using var scope = accessor.BeginScope(
            new OperationContext(Guid.CreateVersion7(), $"simulation:{Guid.CreateVersion7()}")
        );

        var reserved = await quotaService.ReserveAsync(
            new ReserveQuotaRequest(accountId, DemoUsage.Extraction, DemoUsage.Page, 1, "demo")
        );
        if (reserved.ResultCode != ReserveQuotaResultCode.Success)
            return $"Refused: {reserved.Message}";

        await quotaService.SettleAsync(
            new SettleQuotaRequest(accountId, DemoUsage.Extraction, DemoUsage.Page, pages)
        );
        return $"Used {pages:N0} pages.";
    }
}
