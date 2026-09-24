using Corely.Billing.Usage;

namespace Corely.Billing.IntegrationTests.Infrastructure;

internal static class TestUsage
{
    internal static UsageOperation Extraction { get; } = UsageOperation.From("extraction");
    internal static UsageOperation NoOp { get; } = UsageOperation.From("noop");
    internal static UsageOperation Other { get; } = UsageOperation.From("other");

    internal static UsageUnit Page { get; } = UsageUnit.From("page");
    internal static UsageUnit Byte { get; } = UsageUnit.From("byte");
    internal static UsageUnit Document { get; } = UsageUnit.From("document");

    extension(BillingOptions options)
    {
        internal BillingOptions RegisterTestUsage() =>
            options
                .RegisterOperation(Extraction.Value, "Extraction")
                .RegisterOperation(NoOp.Value, "No-op")
                .RegisterOperation(Other.Value, "Other")
                .RegisterUnit(Page.Value, "page")
                .RegisterUnit(Byte.Value, "byte")
                .RegisterUnit(Document.Value, "document");
    }
}
