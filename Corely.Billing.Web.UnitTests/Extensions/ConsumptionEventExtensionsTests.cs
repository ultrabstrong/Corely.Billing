using Corely.Billing.Consumption.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class ConsumptionEventExtensionsTests : BillingWebTestContext
{
    private static readonly DateTime LiveFrom = Now.AddHours(-6);
    private static readonly Guid GrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(ConsumptionOutcome.Settled, 0, "settled")]
    [InlineData(ConsumptionOutcome.Released, 0, "released")]
    [InlineData(null, 1, "held")]
    [InlineData(null, -1, "held_expired")]
    public void CsvStatus_NamesTheLedgerState_ForEachOutcome(
        ConsumptionOutcome? outcome,
        int hoursAfterLiveFrom,
        string expected
    )
    {
        var row = Row();
        row.Outcome = outcome;
        row.FinalizedUtc = outcome is null ? null : Now;
        row.UtcTimestamp = LiveFrom.AddHours(hoursAfterLiveFrom);

        Assert.Equal(expected, row.CsvStatus(LiveFrom));
    }

    [Fact]
    public void ToCsvRow_WritesEveryColumnInHeaderOrder_ForASettledRow()
    {
        var row = Row();
        var vocabulary = Services.GetRequiredService<IUsageVocabulary>();

        var cells = row.ToCsvRow(vocabulary, LiveFrom).Split(',');

        Assert.Equal(ConsumptionEventExtensions.CSV_HEADER.Split(',').Length, cells.Length);
        Assert.Equal(
            [
                row.ConsumptionId.ToString(),
                "2026-03-01T10:00:00.000Z",
                AccountId.ToString(),
                "document_extraction",
                "Document extraction",
                "page",
                "page",
                "12",
                "settled",
                "true",
                "2026-03-01T10:05:00.000Z",
                "text-model",
                GrantId.ToString(),
                "chat:7/turn:2",
                row.CorrelationId.ToString(),
                "",
                "\"{\"\"team\"\":\"\"blue\"\"}\"",
            ],
            cells
        );
    }

    private static ConsumptionEvent Row() =>
        new()
        {
            ConsumptionId = Guid.CreateVersion7(),
            AccountId = AccountId,
            GrantId = GrantId,
            Operation = Extraction,
            Unit = Page,
            Quantity = 12,
            Provider = "text-model",
            UtcTimestamp = Now.AddHours(-2),
            CorrelationId = Guid.CreateVersion7(),
            IdempotencyKey = $"chat:7/turn:2|{Extraction}|{Page}|{GrantId:N}",
            FinalizedUtc = Now.AddHours(-2).AddMinutes(5),
            Outcome = ConsumptionOutcome.Settled,
            Tags = new() { ["team"] = "blue" },
        };
}
