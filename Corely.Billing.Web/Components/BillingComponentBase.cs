using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public abstract class BillingComponentBase : OwningComponentBase
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    protected T Service<T>()
        where T : notnull => ScopedServices.GetRequiredService<T>();

    protected async Task SerializedAsync(Func<Task> work)
    {
        await _gate.WaitAsync();
        try
        {
            await work();
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _gate.Dispose();
        base.Dispose(disposing);
    }
}
