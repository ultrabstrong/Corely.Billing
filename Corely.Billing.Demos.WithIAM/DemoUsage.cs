using Corely.Billing.Usage;

namespace Corely.Billing.Demos.WithIAM;

internal static class DemoUsage
{
    public static readonly UsageOperation TextGeneration = UsageOperation.From("text_generation");
    public static readonly UsageUnit Token = UsageUnit.From("token");
}
