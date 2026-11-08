using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.UnitTests;

public class BillingOperationScopesTests
{
    private readonly List<ScopeProbe> _probes = [];
    private readonly BillingOperationScopes _scopes;

    public BillingOperationScopesTests()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ =>
        {
            var probe = new ScopeProbe();
            _probes.Add(probe);
            return probe;
        });
        services.AddScoped<IBillingAccountAccessor>(sp => new SigningInAccessor(
            sp.GetRequiredService<ScopeProbe>()
        ));
        _scopes = new BillingOperationScopes(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>()
        );
    }

    [Fact]
    public async Task RunAsync_SignsEachScopeIn_ThroughTheAccountAccessor()
    {
        var signedIn = await _scopes.RunAsync(s =>
            Task.FromResult(s.GetRequiredService<ScopeProbe>().SignedIn)
        );

        Assert.True(signedIn);
    }

    [Fact]
    public async Task RunAsync_GivesEachOperationItsOwnScope_ForTwoCalls()
    {
        var first = await _scopes.RunAsync(s =>
            Task.FromResult(s.GetRequiredService<ScopeProbe>())
        );
        var second = await _scopes.RunAsync(s =>
            Task.FromResult(s.GetRequiredService<ScopeProbe>())
        );

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task RunAsync_DisposesTheScope_AfterTheWork()
    {
        Assert.True(await _scopes.RunAsync(_ => Task.CompletedTask));

        Assert.True(Assert.Single(_probes).Disposed);
    }

    [Fact]
    public async Task RunAsync_DisposesTheScopeAndRethrows_ForThrowingWork()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _scopes.RunAsync(_ => throw new InvalidOperationException())
        );

        Assert.True(Assert.Single(_probes).Disposed);
    }

    private sealed class ScopeProbe : IDisposable
    {
        public bool SignedIn { get; set; }
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class SigningInAccessor(ScopeProbe probe) : IBillingAccountAccessor
    {
        public Task<Guid?> GetAccountIdAsync()
        {
            probe.SignedIn = true;
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }
}
