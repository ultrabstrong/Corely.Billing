using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.Components;

/// <summary>
/// A Billing component that calls services. Each action runs through <see cref="InOperationAsync"/>
/// in a signed in DI scope of its own, so no read returns what an earlier action loaded.
/// </summary>
public abstract class BillingOperationComponentBase : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IBillingOperationScopes OperationScopes { get; set; } = null!;

    /// <inheritdoc cref="IBillingOperationScopes.RunAsync{T}"/>
    protected Task<T?> InOperationAsync<T>(Func<IServiceProvider, Task<T>> work) =>
        OperationScopes.RunAsync(work);

    /// <inheritdoc cref="IBillingOperationScopes.RunAsync(Func{IServiceProvider, Task})"/>
    protected Task<bool> InOperationAsync(Func<IServiceProvider, Task> work) =>
        OperationScopes.RunAsync(work);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases what a derived component holds; call the base when overriding. Blazor disposes a
    /// component through <see cref="DisposeAsync"/> only, so a derived component overrides this
    /// rather than implementing <see cref="IDisposable"/>.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}
