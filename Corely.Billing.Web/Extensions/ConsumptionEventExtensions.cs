using System.Globalization;
using System.Text.Json;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.Extensions;

internal static class ConsumptionEventExtensions
{
    public const string CSV_HEADER =
        "consumption_id,occurred_utc,account_id,operation,operation_name,unit,unit_name,quantity,"
        + "status,counts_toward_balance,finalized_utc,provider,grant_id,work_id,correlation_id,"
        + "user_id,tags";

    extension(ConsumptionEvent row)
    {
        public string CsvStatus(DateTime liveFromUtc) =>
            row.Outcome switch
            {
                ConsumptionOutcome.Settled => "settled",
                ConsumptionOutcome.Released => "released",
                _ => row.CountsTowardBalance(liveFromUtc) ? "held" : "held_expired",
            };

        public string ToCsvRow(IUsageVocabulary vocabulary, DateTime liveFromUtc) =>
            new[]
            {
                row.ConsumptionId.ToString(),
                row.UtcTimestamp.IsoUtc(),
                row.AccountId.ToString(),
                row.Operation.Value,
                vocabulary.DisplayName(row.Operation),
                row.Unit.Value,
                vocabulary.DisplayName(row.Unit),
                row.Quantity.ToString(CultureInfo.InvariantCulture),
                row.CsvStatus(liveFromUtc),
                row.CountsTowardBalance(liveFromUtc) ? "true" : "false",
                row.FinalizedUtc?.IsoUtc(),
                row.Provider,
                row.GrantId.ToString(),
                row.IdempotencyScope,
                row.CorrelationId.ToString(),
                row.UserId?.ToString(),
                row.Tags is { Count: > 0 } ? JsonSerializer.Serialize(row.Tags) : null,
            }.ToCsvRow();
    }
}
