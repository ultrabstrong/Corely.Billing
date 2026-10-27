using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.IntegrationTests.Infrastructure;

internal static class TestUsage
{
    internal static UsageOperation Extraction { get; } = UsageOperation.From("extraction");
    internal static UsageOperation NoOp { get; } = UsageOperation.From("noop");
    internal static UsageOperation Other { get; } = UsageOperation.From("other");
    internal static UsageOperation Members { get; } = UsageOperation.From("members");
    internal static UsageOperation Devices { get; } = UsageOperation.From("devices");
    internal static UsageOperation Retention { get; } = UsageOperation.From("retention");
    internal static UsageOperation Sftp { get; } = UsageOperation.From("sftp");

    internal static UsageUnit Page { get; } = UsageUnit.From("page");
    internal static UsageUnit Byte { get; } = UsageUnit.From("byte");
    internal static UsageUnit Document { get; } = UsageUnit.From("document");
    internal static UsageUnit User { get; } = UsageUnit.From("user");
    internal static UsageUnit Device { get; } = UsageUnit.From("device");
    internal static UsageUnit Day { get; } = UsageUnit.From("day");

    extension(BillingOptions options)
    {
        internal BillingOptions RegisterTestUsage() =>
            options
                .RegisterUnit(Page.Value, "page")
                .RegisterUnit(Byte.Value, "byte")
                .RegisterUnit(Document.Value, "document")
                .RegisterUnit(User.Value, "user")
                .RegisterUnit(Device.Value, "device")
                .RegisterUnit(Day.Value, "day")
                .RegisterOperation(Extraction.Value, "Extraction", GrantKind.Consumable, Page.Value)
                .RegisterOperation(NoOp.Value, "No-op", GrantKind.Consumable, Page.Value)
                .RegisterOperation(Other.Value, "Other", GrantKind.Consumable, Document.Value)
                .RegisterOperation(Members.Value, "Members", GrantKind.Capacity, User.Value)
                .RegisterOperation(Devices.Value, "Devices", GrantKind.Capacity, Device.Value)
                .RegisterOperation(Retention.Value, "Retention", GrantKind.Allowance, Day.Value)
                .RegisterOperation(Sftp.Value, "SFTP", GrantKind.Feature);
    }
}
