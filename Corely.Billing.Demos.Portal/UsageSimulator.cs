using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;

namespace Corely.Billing.Demos.Portal;

internal sealed class UsageSimulator(IQuotaService quotaService, IOperationContextAccessor accessor)
{
    // A reply's length is unknown until the model answers, so the hold is an estimate the
    // settlement corrects. Text to embed is counted before it is sent, so its hold is exact.
    private const long ESTIMATED_TOKENS = 1_000;

    public Task<string> GenerateTextAsync(long tokens) =>
        RunAsync(DemoUsage.TextGeneration, ESTIMATED_TOKENS, tokens, "text-model");

    public Task<string> EmbedAsync(long tokens) =>
        RunAsync(DemoUsage.Embeddings, tokens, tokens, "embedding-model");

    private async Task<string> RunAsync(
        UsageOperation operation,
        long estimate,
        long actual,
        string provider
    )
    {
        using var scope = accessor.BeginScope(
            new OperationContext(Guid.CreateVersion7(), $"request:{Guid.CreateVersion7()}")
        );

        var reserved = await quotaService.ReserveAsync(
            new ReserveQuotaRequest(
                DemoUsage.AccountId,
                operation,
                DemoUsage.Token,
                estimate,
                provider
            )
        );
        if (reserved.ResultCode != ReserveQuotaResultCode.Success)
            return $"Refused: {reserved.Message}";

        var settled = await quotaService.SettleAsync(
            new SettleQuotaRequest(DemoUsage.AccountId, operation, DemoUsage.Token, actual)
        );
        return settled.Overdrawn
            ? $"Used {actual:N0} tokens and overdrew the last grant."
            : $"Used {actual:N0} tokens.";
    }
}
