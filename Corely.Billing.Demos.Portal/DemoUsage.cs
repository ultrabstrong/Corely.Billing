using Corely.Billing.Usage;

namespace Corely.Billing.Demos.Portal;

internal static class DemoUsage
{
    public static readonly Guid AccountId = Guid.Parse("0199a0de-0000-7000-8000-00000000d3e0");

    public const string PROVIDER = "demo";

    public static readonly UsageOperation Extraction = UsageOperation.From("document_extraction");
    public static readonly UsageOperation Summaries = UsageOperation.From("summaries");
    public static readonly UsageUnit Page = UsageUnit.From("page");

    public static BillingOptions Register(BillingOptions options) =>
        options
            .RegisterOperation(Extraction.Value, "Document extraction")
            .RegisterOperation(Summaries.Value, "Summaries")
            .RegisterUnit(Page.Value, "page");
}
