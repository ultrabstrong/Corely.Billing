using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Corely.Billing.Web.Components;

public abstract class BillingComponentBase : OwningComponentBase
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private UsageExporter? _exporter;

    internal UsageExporter Exporter =>
        _exporter ??= ActivatorUtilities.CreateInstance<UsageExporter>(ScopedServices, JSRuntime);

    protected override async Task OnInitializedAsync() =>
        await ScopedServices.GetRequiredService<IBillingAccountAccessor>().GetAccountIdAsync();

    protected override async ValueTask DisposeAsyncCore()
    {
        if (_exporter is not null)
            await _exporter.DisposeAsync();
        await base.DisposeAsyncCore();
    }
}
