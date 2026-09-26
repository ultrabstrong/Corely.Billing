using Corely.Billing.Usage;

namespace Corely.Billing.Demos.WithIAM;

internal static class DemoUsage
{
    public static readonly UsageOperation Extraction = UsageOperation.From("document_extraction");
    public static readonly UsageUnit Page = UsageUnit.From("page");
}
