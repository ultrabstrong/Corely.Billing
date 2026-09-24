using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.Extensions;

internal static class GrantExtensions
{
    extension(Grant grant)
    {
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
