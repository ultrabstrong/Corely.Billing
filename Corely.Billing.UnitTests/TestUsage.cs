using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.UnitTests;

internal static class TestUsage
{
    internal static UsageOperation Extraction { get; } = UsageOperation.From("extraction");
    internal static UsageOperation NoOp { get; } = UsageOperation.From("noop");
    internal static UsageOperation Other { get; } = UsageOperation.From("other");
    internal static UsageOperation Members { get; } = UsageOperation.From("members");
    internal static UsageOperation Retention { get; } = UsageOperation.From("retention");
    internal static UsageOperation Sftp { get; } = UsageOperation.From("sftp");

    internal static UsageUnit Page { get; } = UsageUnit.From("page");
    internal static UsageUnit Byte { get; } = UsageUnit.From("byte");
    internal static UsageUnit Document { get; } = UsageUnit.From("document");
    internal static UsageUnit User { get; } = UsageUnit.From("user");
    internal static UsageUnit Day { get; } = UsageUnit.From("day");

    internal static UsageOperation Unregistered { get; } = UsageOperation.From("never_registered");
    internal static UsageUnit UnregisteredUnit { get; } = UsageUnit.From("never_registered");

    private static readonly UsageOperationDefinition[] Operations =
    [
        new(Extraction, "Extraction", GrantKind.Consumable, Page),
        new(NoOp, "No-op", GrantKind.Consumable, Page),
        new(Other, "Other", GrantKind.Consumable, Document),
        new(Members, "Members", GrantKind.Capacity, User),
        new(Retention, "Retention", GrantKind.Allowance, Day),
        new(Sftp, "SFTP", GrantKind.Feature, null),
    ];

    extension(BillingOptions options)
    {
        internal BillingOptions RegisterTestUsage()
        {
            options
                .RegisterUnit(Page.Value, "page")
                .RegisterUnit(Byte.Value, "byte")
                .RegisterUnit(Document.Value, "document")
                .RegisterUnit(User.Value, "user")
                .RegisterUnit(Day.Value, "day");
            foreach (var operation in Operations)
            {
                options.RegisterOperation(
                    operation.Operation.Value,
                    operation.DisplayName,
                    operation.Kind,
                    operation.Unit?.Value
                );
            }
            return options;
        }
    }

    internal static IUsageVocabulary Vocabulary { get; } =
        new UsageVocabulary(
            Operations,
            [
                new(Page, "page"),
                new(Byte, "byte"),
                new(Document, "document"),
                new(User, "user"),
                new(Day, "day"),
            ]
        );
}
