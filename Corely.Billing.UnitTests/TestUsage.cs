using Corely.Billing.Usage;

namespace Corely.Billing.UnitTests;

internal static class TestUsage
{
    internal static UsageOperation Extraction { get; } = UsageOperation.From("extraction");
    internal static UsageOperation NoOp { get; } = UsageOperation.From("noop");
    internal static UsageOperation Other { get; } = UsageOperation.From("other");

    internal static UsageUnit Page { get; } = UsageUnit.From("page");
    internal static UsageUnit Byte { get; } = UsageUnit.From("byte");
    internal static UsageUnit Document { get; } = UsageUnit.From("document");

    internal static UsageOperation Unregistered { get; } = UsageOperation.From("never_registered");
    internal static UsageUnit UnregisteredUnit { get; } = UsageUnit.From("never_registered");

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

    internal static IUsageVocabulary Vocabulary { get; } =
        new UsageVocabulary(
            [new(Extraction, "Extraction"), new(NoOp, "No-op"), new(Other, "Other")],
            [new(Page, "page"), new(Byte, "byte"), new(Document, "document")]
        );
}
