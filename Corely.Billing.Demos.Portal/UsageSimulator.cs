using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web;

namespace Corely.Billing.Demos.Portal;

internal sealed class UsageSimulator(IQuotaService quotaService, IOperationContextAccessor accessor)
{
    // A reply's length is unknown until the model answers, so the hold is an estimate the
    // settlement corrects. Text to embed is counted before it is sent, and an image count is
    // asked for up front, so those holds are exact.
    private const long ESTIMATED_TOKENS = 1_000;

    public Task<string> GenerateTextAsync(long tokens, string model = "fast-model") =>
        RunAsync(DemoUsage.TextGeneration, DemoUsage.Token, ESTIMATED_TOKENS, tokens, model);

    public Task<string> EmbedAsync(long tokens) =>
        RunAsync(DemoUsage.Embeddings, DemoUsage.Token, tokens, tokens, "embedding-model");

    public Task<string> GenerateImagesAsync(long images) =>
        RunAsync(DemoUsage.ImageGeneration, DemoUsage.Image, images, images, "image-model");

    private async Task<string> RunAsync(
        UsageOperation operation,
        UsageUnit unit,
        long estimate,
        long actual,
        string provider
    )
    {
        using var scope = accessor.BeginScope(
            new OperationContext(Guid.CreateVersion7(), $"request:{Guid.CreateVersion7()}")
        );

        var reserved = await quotaService.ReserveAsync(
            new ReserveQuotaRequest(DemoUsage.AccountId, operation, unit, estimate, provider)
        );
        if (reserved.ResultCode != ReserveQuotaResultCode.Success)
            return $"Refused: {reserved.Message}";

        var settled = await quotaService.SettleAsync(
            new SettleQuotaRequest(DemoUsage.AccountId, operation, unit, actual)
        );
        return settled.Overdrawn
            ? $"Used {UsageText.Count(actual, unit.Value)} and overdrew the last grant."
            : $"Used {UsageText.Count(actual, unit.Value)}.";
    }
}
