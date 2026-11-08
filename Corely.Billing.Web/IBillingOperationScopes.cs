namespace Corely.Billing.Web;

/// <summary>
/// Runs one user visible action of a Billing component in a DI scope of its own, signed in through
/// <see cref="IBillingAccountAccessor"/> and disposed when the action ends, so every read sees the
/// database as it is now. Corely.Billing.Web.IAM replaces it with Corely.IAM.Web's operation scopes.
/// </summary>
public interface IBillingOperationScopes
{
    /// <summary>
    /// Runs <paramref name="work"/> in a new signed in scope and returns its result, or
    /// <c>default</c> without running it when the scope cannot be signed in.
    /// </summary>
    Task<T?> RunAsync<T>(Func<IServiceProvider, Task<T>> work);

    /// <summary>
    /// Runs <paramref name="work"/> in a new signed in scope. Returns <c>false</c>, without running
    /// it, when the scope cannot be signed in.
    /// </summary>
    Task<bool> RunAsync(Func<IServiceProvider, Task> work);
}
