using Corely.Billing.Usage;

namespace Corely.Billing.Demos.Portal;

internal static class DemoUsage
{
    public static readonly Guid AccountId = Guid.Parse("0199a0de-0000-7000-8000-00000000d3e0");

    public static readonly UsageOperation TextGeneration = UsageOperation.From("text_generation");
    public static readonly UsageOperation Embeddings = UsageOperation.From("embeddings");
    public static readonly UsageUnit Token = UsageUnit.From("token");

    public static BillingOptions Register(BillingOptions options) =>
        options
            .RegisterOperation(TextGeneration.Value, "Text generation")
            .RegisterOperation(Embeddings.Value, "Embeddings")
            .RegisterUnit(Token.Value, "token");
}
