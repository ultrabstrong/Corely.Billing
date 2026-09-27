using Corely.Billing.Usage;

namespace Corely.Billing.Web.Components;

public sealed record UsageFilter(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<UsageUnit>? Units = null,
    IReadOnlyList<UsageOperation>? Operations = null,
    IReadOnlyList<string>? Providers = null,
    IReadOnlyList<Guid>? GrantIds = null
)
{
    internal string Signature =>
        string.Join(
            '|',
            FromUtc.Ticks,
            ToUtc.Ticks,
            Join(Units?.Select(u => u.Value)),
            Join(Operations?.Select(o => o.Value)),
            Join(Providers),
            Join(GrantIds?.Select(g => g.ToString()))
        );

    public IEnumerable<string> Describe(IUsageVocabulary vocabulary)
    {
        yield return $"From {FromUtc:yyyy-MM-dd HH:mm} UTC to {ToUtc:yyyy-MM-dd HH:mm} UTC";
        yield return $"Units: {Names(Units?.Select(vocabulary.DisplayName))}";
        yield return $"Operations: {Names(Operations?.Select(vocabulary.DisplayName))}";
        yield return $"Providers: {Names(Providers)}";
        yield return $"Grants: {Names(GrantIds?.Select(g => g.ToString()))}";
    }

    private static string Names(IEnumerable<string>? values) =>
        values is null ? "all" : string.Join(", ", values);

    private static string Join(IEnumerable<string>? values) =>
        values is null ? string.Empty : string.Join(',', values.Order());
}
