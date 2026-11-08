using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Corely.Billing.Web.Components;

public abstract class BillingComponentBase : BillingOperationComponentBase
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    // The exporter reads through the export's own operation, and the circuit's JS runtime downloads.
    internal Task<ExportOutcome?> RunExportAsync(Func<UsageExporter, Task<ExportOutcome>> export) =>
        InOperationAsync(async services =>
        {
            await using var exporter = ActivatorUtilities.CreateInstance<UsageExporter>(
                services,
                JSRuntime
            );
            return await export(exporter);
        });
}
