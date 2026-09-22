using Corely.Billing;

namespace Corely.Billing.UnitTests;

internal static class TestUsage
{
    internal static UsageOperation Extraction { get; } = UsageOperation.From("extraction");
    internal static UsageOperation NoOp { get; } = UsageOperation.From("noop");
    internal static UsageOperation Other { get; } = UsageOperation.From("other");
    internal static UsageOperation Unknown { get; } = UsageOperation.From("unknown");

    internal static UsageUnit Page { get; } = UsageUnit.From("page");
    internal static UsageUnit Byte { get; } = UsageUnit.From("byte");
    internal static UsageUnit Document { get; } = UsageUnit.From("document");
    internal static UsageUnit Field { get; } = UsageUnit.From("field");
    internal static UsageUnit OtherUnit { get; } = UsageUnit.From("other_unit");

    internal static IUsageVocabulary Vocabulary { get; } =
        new UsageVocabularyBuilder()
            .Operation(Extraction.Value, "Extraction")
            .Operation(NoOp.Value, "No-op")
            .Operation(Other.Value, "Other")
            .Operation(Unknown.Value, "Unknown")
            .Unit(Page.Value, "page")
            .Unit(Byte.Value, "byte")
            .Unit(Document.Value, "document")
            .Unit(Field.Value, "field")
            .Unit(OtherUnit.Value, "other unit")
            .Build();
}
