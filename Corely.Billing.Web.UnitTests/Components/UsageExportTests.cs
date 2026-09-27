using System.IO.Compression;
using System.Text;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageExportTests : BillingWebTestContext
{
    [Fact]
    public void ToZip_HoldsEveryFileAndAReadme_ForAChartedExport()
    {
        var export = Export(included: 3, matched: 3, chartCsv: "c\r\n");

        using var zip = new ZipArchive(new MemoryStream(export.ToZip(Vocabulary())));

        Assert.Equal(
            [
                UsageExport.README_FILE,
                UsageExport.EVENTS_FILE,
                UsageExport.GRANTS_FILE,
                UsageExport.CHART_FILE,
            ],
            zip.Entries.Select(e => e.FullName)
        );
    }

    [Fact]
    public void ToZip_LeavesOutTheChart_ForNoChartData()
    {
        var export = Export(included: 3, matched: 3, chartCsv: null);

        using var zip = new ZipArchive(new MemoryStream(export.ToZip(Vocabulary())));

        Assert.DoesNotContain(zip.Entries, e => e.FullName == UsageExport.CHART_FILE);
    }

    [Fact]
    public void Readme_SaysSoAndHow_ForACappedExport()
    {
        var export = Export(included: 100_000, matched: 134_210, chartCsv: null);

        var readme = export.Readme(Vocabulary());

        Assert.True(export.IsCapped);
        Assert.Contains("**Capped.** This export stopped at 100,000 of 134,210 events.", readme);
        Assert.Contains("Narrow the date range", readme);
    }

    [Fact]
    public void Readme_DescribesTheFilterAndEveryColumn_ForAnyExport()
    {
        var readme = Export(included: 1, matched: 1, chartCsv: null).Readme(Vocabulary());

        Assert.Contains("Complete: all 1 matching events are included.", readme);
        Assert.Contains("Operations: Document extraction", readme);
        foreach (
            var column in Corely.Billing.Web.Extensions.ConsumptionEventExtensions.CSV_HEADER.Split(
                ','
            )
        )
            Assert.Contains($"`{column}`", readme);
    }

    [Fact]
    public void CsvBytes_StartsWithAByteOrderMark_ForExcel()
    {
        var bytes = UsageExport.CsvBytes("a");

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
    }

    private IUsageVocabulary Vocabulary() => Services.GetRequiredService<IUsageVocabulary>();

    private static UsageExport Export(int included, int matched, string? chartCsv) =>
        new(
            AccountId,
            new UsageFilter(Now.AddDays(-7), Now, Operations: [Extraction]),
            Now,
            TimeSpan.FromHours(6),
            "e\r\n",
            included,
            matched,
            "g\r\n",
            chartCsv,
            chartCsv is null ? null : "Used"
        );
}
