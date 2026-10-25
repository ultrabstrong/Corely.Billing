using System.Globalization;
using System.Text.Json;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.Extensions;

internal static class GrantExtensions
{
    public const string CSV_HEADER =
        "grant_id,operation,operation_name,kind,type,unit,unit_name,quantity,unlimited,"
        + "valid_from_utc,valid_to_utc,status,used,remaining,in_use,tags";

    extension(Grant grant)
    {
        public string ToCsvRow(IUsageVocabulary vocabulary, DateTime utcNow) =>
            new[]
            {
                grant.GrantId.ToString(),
                grant.Operation.Value,
                vocabulary.DisplayName(grant.Operation),
                grant.Kind.ToString().ToLowerInvariant(),
                grant.GetGrantType().ToString().ToLowerInvariant(),
                grant.Unit?.Value,
                grant.Unit is { } unit ? vocabulary.DisplayName(unit) : null,
                grant.Quantity?.ToString(CultureInfo.InvariantCulture),
                grant.Kind != GrantKind.Feature && grant.Quantity is null ? "true" : "false",
                grant.ValidFromUtc.IsoUtc(),
                grant.ValidToUtc.IsoUtc(),
                grant.Status(utcNow).ToString().ToLowerInvariant(),
                grant.Used?.ToString(CultureInfo.InvariantCulture),
                grant.Remaining?.ToString(CultureInfo.InvariantCulture),
                grant.InUse?.ToString(CultureInfo.InvariantCulture),
                grant.Tags is { Count: > 0 } ? JsonSerializer.Serialize(grant.Tags) : null,
            }.ToCsvRow();

        public GrantStatus Status(DateTime utcNow) =>
            grant.Disabled ? GrantStatus.Disabled : grant.Window(utcNow);

        public GrantStatus Window(DateTime utcNow) =>
            utcNow < grant.ValidFromUtc ? GrantStatus.Upcoming
            : utcNow > grant.ValidToUtc ? GrantStatus.Expired
            : GrantStatus.Active;

        public string WhenText(DateTime utcNow) =>
            grant.Window(utcNow) switch
            {
                GrantStatus.Upcoming => $"Starts {(grant.ValidFromUtc - utcNow).AheadText()}",
                GrantStatus.Active when grant.NeverExpires() => "Never expires",
                GrantStatus.Active => $"Expires {(grant.ValidToUtc - utcNow).AheadText()}",
                _ => $"Expired {(utcNow - grant.ValidToUtc).AgoText()}",
            };

        public string WindowText() =>
            grant.NeverExpires()
                ? $"From {grant.ValidFromUtc:MMM d, yyyy}, never expires"
                : $"{grant.ValidFromUtc:MMM d, yyyy} to {grant.ValidToUtc:MMM d, yyyy}";

        public string Headline(IUsageVocabulary vocabulary) =>
            grant.Kind == GrantKind.Feature
                ? vocabulary.DisplayName(grant.Operation)
                : UsageText.Count(grant.Quantity, grant.UnitName(vocabulary));

        public string StateText(IUsageVocabulary vocabulary, DateTime utcNow) =>
            grant.GetGrantType() switch
            {
                GrantType.Unlimited =>
                    $"Unlimited, {UsageText.Count(grant.Used ?? 0, grant.UnitName(vocabulary))} used",
                GrantType.Seats =>
                    $"{grant.InUse ?? 0:N0} of {UsageText.Count(grant.Quantity!.Value, grant.UnitName(vocabulary))} in use",
                GrantType.UnlimitedSeats => $"Unlimited, {grant.InUse ?? 0:N0} in use",
                GrantType.Allowance =>
                    $"+{UsageText.Count(grant.Quantity!.Value, grant.UnitName(vocabulary))}{grant.UntilText()}",
                GrantType.UnlimitedAllowance =>
                    $"Unlimited {grant.UnitName(vocabulary)}s{grant.UntilText()}",
                GrantType.Feature => grant.Status(utcNow) == GrantStatus.Active
                    ? $"Enabled{grant.UntilText()}"
                    : "Disabled",
                _ => UsageText.Count(grant.Remaining ?? 0, grant.UnitName(vocabulary)) + " left",
            };

        public string UntilText() =>
            grant.NeverExpires() ? ", with no end date" : $", until {grant.ValidToUtc:d MMMM yyyy}";

        public string HeadlineAndExpiry(IUsageVocabulary vocabulary) =>
            $"{grant.Headline(vocabulary)} to {grant.ValidToUtc:MMM d, yyyy}";

        public string HeadlineAndWindow(IUsageVocabulary vocabulary) =>
            $"{grant.Headline(vocabulary)}, {grant.WindowText()}";

        public string UnitName(IUsageVocabulary vocabulary) =>
            grant.Unit is { } unit ? vocabulary.DisplayName(unit) : "seat";
    }
}
