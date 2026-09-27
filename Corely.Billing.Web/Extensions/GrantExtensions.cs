using System.Globalization;
using System.Text.Json;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.Extensions;

internal static class GrantExtensions
{
    public const string CSV_HEADER =
        "grant_id,operation,operation_name,unit,unit_name,quantity,unlimited,valid_from_utc,"
        + "valid_to_utc,status,used,remaining,overdrawn_by,tags";

    extension(Grant grant)
    {
        public string ToCsvRow(IUsageVocabulary vocabulary, long used, DateTime utcNow)
        {
            var balance = GrantBalance.For(grant, used);
            return new[]
            {
                grant.GrantId.ToString(),
                grant.Operation.Value,
                vocabulary.DisplayName(grant.Operation),
                grant.Unit.Value,
                vocabulary.DisplayName(grant.Unit),
                grant.Quantity?.ToString(CultureInfo.InvariantCulture),
                balance.IsUnlimited ? "true" : "false",
                grant.ValidFromUtc.IsoUtc(),
                grant.ValidToUtc.IsoUtc(),
                grant.Status(utcNow).ToString().ToLowerInvariant(),
                used.ToString(CultureInfo.InvariantCulture),
                balance.IsUnlimited
                    ? null
                    : balance.Remaining.ToString(CultureInfo.InvariantCulture),
                balance.Overdraft.ToString(CultureInfo.InvariantCulture),
                grant.Tags is { Count: > 0 } ? JsonSerializer.Serialize(grant.Tags) : null,
            }.ToCsvRow();
        }

        public GrantStatus Status(DateTime utcNow) =>
            utcNow < grant.ValidFromUtc ? GrantStatus.Upcoming
            : utcNow > grant.ValidToUtc ? GrantStatus.Expired
            : GrantStatus.Active;

        public string WhenText(DateTime utcNow) =>
            grant.Status(utcNow) switch
            {
                GrantStatus.Upcoming => $"Starts {(grant.ValidFromUtc - utcNow).AheadText()}",
                GrantStatus.Active => $"Expires {(grant.ValidToUtc - utcNow).AheadText()}",
                _ => $"Expired {(utcNow - grant.ValidToUtc).AgoText()}",
            };

        public string Allowance(IUsageVocabulary vocabulary) =>
            UsageText.Count(grant.Quantity, vocabulary.DisplayName(grant.Unit));

        public string AllowanceAndExpiry(IUsageVocabulary vocabulary) =>
            $"{grant.Allowance(vocabulary)} to {grant.ValidToUtc:MMM d, yyyy}";

        public string AllowanceAndWindow(IUsageVocabulary vocabulary) =>
            $"{grant.Allowance(vocabulary)}, {grant.ValidFromUtc:MMM d, yyyy} – {grant.ValidToUtc:MMM d, yyyy}";
    }
}
