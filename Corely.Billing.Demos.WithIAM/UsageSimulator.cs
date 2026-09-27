using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;

namespace Corely.Billing.Demos.WithIAM;

internal sealed class UsageSimulator(IQuotaService quotaService, IOperationContextAccessor accessor)
{
    // A reply's length is unknown until the model answers, so the hold is an estimate the
    // settlement corrects.
    private const long ESTIMATED_TOKENS = 1_000;

    public async Task<string> GenerateTextAsync(Guid accountId, long tokens)
    {
        using var scope = accessor.BeginScope(
            new OperationContext(Guid.CreateVersion7(), $"request:{Guid.CreateVersion7()}")
        );

        var reserved = await quotaService.ReserveAsync(
            new ReserveQuotaRequest(
                accountId,
                DemoUsage.TextGeneration,
                DemoUsage.Token,
                ESTIMATED_TOKENS,
                "text-model"
            )
        );
        if (reserved.ResultCode != ReserveQuotaResultCode.Success)
            return $"Refused: {reserved.Message}";

        await quotaService.SettleAsync(
            new SettleQuotaRequest(accountId, DemoUsage.TextGeneration, DemoUsage.Token, tokens)
        );
        return $"Used {tokens:N0} tokens.";
    }
}
