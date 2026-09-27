using System.IO.Compression;
using System.Text;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.Components;

// One export: the ledger rows, the grants, the chart's series, and a README that says what they
// mean, for whoever (or whatever) analyses them next.
internal sealed record UsageExport(
    Guid AccountId,
    UsageFilter Filter,
    DateTime TakenUtc,
    TimeSpan ReservationTtl,
    string EventsCsv,
    int EventsIncluded,
    int EventsMatched,
    string GrantsCsv,
    string? ChartCsv,
    string? ChartCaption
)
{
    public const int MAX_EVENTS = 100_000;
    public const string EVENTS_FILE = "usage-events.csv";
    public const string GRANTS_FILE = "grants.csv";
    public const string CHART_FILE = "chart-series.csv";
    public const string README_FILE = "README.md";

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public bool IsCapped => EventsIncluded < EventsMatched;

    public static string CappedNotice(int included, int matched) =>
        $"This export stopped at {included:N0} of {matched:N0} events. "
        + "Narrow the date range to get the rest.";

    public static byte[] CsvBytes(string csv) =>
        [.. Utf8WithBom.GetPreamble(), .. Utf8WithBom.GetBytes(csv)];

    public string FileName => $"usage-{TakenUtc:yyyyMMdd-HHmmss}.zip";

    public byte[] ToZip(IUsageVocabulary vocabulary)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, README_FILE, Encoding.UTF8.GetBytes(Readme(vocabulary)));
            Add(zip, EVENTS_FILE, CsvBytes(EventsCsv));
            Add(zip, GRANTS_FILE, CsvBytes(GrantsCsv));
            if (ChartCsv is not null)
                Add(zip, CHART_FILE, CsvBytes(ChartCsv));
        }
        return stream.ToArray();
    }

    public string Readme(IUsageVocabulary vocabulary)
    {
        var text = new StringBuilder();
        text.AppendLine("# Usage export");
        text.AppendLine();
        text.AppendLine($"Account `{AccountId}`, taken {TakenUtc.IsoUtc()}. All times are UTC.");
        text.AppendLine();
        text.AppendLine("Filter:");
        foreach (var line in Filter.Describe(vocabulary))
            text.AppendLine($"- {line}");
        text.AppendLine();
        text.AppendLine(
            IsCapped
                ? $"**Capped.** {CappedNotice(EventsIncluded, EventsMatched)} The newest events come first."
                : $"Complete: all {EventsMatched:N0} matching events are included."
        );
        text.AppendLine();
        text.AppendLine(
            $"""
            ## {EVENTS_FILE}

            One row per ledger row. Work is charged by reserving quantity first (a hold) and then
            settling it to what the work cost, or releasing it if the work failed. One charge can
            split across several grants, which writes several rows that share a `work_id`.

            | Column | Meaning |
            |---|---|
            | `consumption_id` | The ledger row |
            | `occurred_utc` | When the row was written |
            | `account_id` | The account charged |
            | `operation`, `operation_name` | What was done: the stored token, and its display name |
            | `unit`, `unit_name` | What it was measured in: the stored token, and its display name |
            | `quantity` | How much the row charged, or holds |
            | `status` | `settled`: final. `held`: reserved, not yet settled. `held_expired`: reserved and never resolved, older than the reservation TTL ({ReservationTtl}). `released`: reserved, then given back because the work failed |
            | `counts_toward_balance` | `true` for settled rows and holds still within the TTL. Sum `quantity` over these rows to get what a grant has used |
            | `finalized_utc` | When a hold was settled or released; empty while it is held |
            | `provider` | Who performed the work |
            | `grant_id` | The grant the row drew on; joins to `{GRANTS_FILE}` |
            | `work_id` | The unit of work the row was charged under. Rows with one `work_id` are one charge |
            | `correlation_id` | Joins to the application's logs |
            | `user_id` | Who asked for the work, when the application recorded it |
            | `tags` | Free-form labels, as a JSON object |

            A released row keeps its original quantity, so what was held stays visible; it does not
            count toward any balance.

            ## {GRANTS_FILE}

            Every grant of the account: what it may use, and when.

            | Column | Meaning |
            |---|---|
            | `grant_id` | The grant |
            | `operation`, `operation_name`, `unit`, `unit_name` | What the grant covers |
            | `quantity` | The allowance; empty when `unlimited` is `true` |
            | `valid_from_utc`, `valid_to_utc` | The window the grant can be drawn on |
            | `status` | `upcoming`, `active` or `expired` when the export was taken |
            | `used` | Everything counted against the grant, in any date range, when the export was taken |
            | `remaining` | `quantity` minus `used`, never below zero; empty when unlimited |
            | `overdrawn_by` | How far `used` went past `quantity`. Work already done is always recorded, so a grant can end up overdrawn |
            | `tags` | Free-form labels, as a JSON object |

            Work draws on unlimited grants first, then on limited grants soonest-expiring first.
            """
        );

        if (ChartCsv is not null)
        {
            text.AppendLine();
            text.AppendLine(
                $"""
                ## {CHART_FILE}

                The chart as it was drawn: {ChartCaption}. One row per period and series:
                `period_start_utc`, `period` (its label), `series_key` and `series_name` (what the
                series is), `quantity`, and `unit`. An empty quantity means the series had no value
                in that period, such as a grant that was not live.
                """
            );
        }

        return text.ToString();
    }

    private static void Add(ZipArchive zip, string name, byte[] content)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(content);
    }
}
