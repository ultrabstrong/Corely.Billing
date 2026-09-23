using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.IntegrationTests.Infrastructure;

public interface IBillingTestHost
{
    FakeTimeProvider TimeProvider { get; }

    Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> work);

    Task<T> InOperationAsync<T>(string idempotencyScope, Func<IServiceProvider, Task<T>> work);
}
