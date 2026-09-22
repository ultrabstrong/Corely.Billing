using Corely.Billing.Operations;

namespace Corely.Billing.UnitTests.Operations;

public class AsyncLocalOperationContextAccessorTests
{
    private static OperationContext Context(string scope) => new(Guid.CreateVersion7(), scope);

    [Fact]
    public void Current_ReturnsNull_ForNoOpenScope()
    {
        // Absence has to be observable. Metering refuses to record anything without a context, and
        // that refusal is the thing standing between a retry and a second charge.
        var accessor = new AsyncLocalOperationContextAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Current_ReturnsTheContext_ForAnOpenScope()
    {
        var accessor = new AsyncLocalOperationContextAccessor();
        var context = Context("scope-a");

        using var scope = accessor.BeginScope(context);

        Assert.Equal(context, accessor.Current);
    }

    [Fact]
    public void Current_RestoresThePreviousContext_ForANestedScopeThatEnds()
    {
        // The Functions middleware opens a baseline scope and the workflow executor nests a
        // job-and-step scope inside it. The inner one has to unwind cleanly or the next invocation
        // on this thread inherits the wrong billing identity.
        var accessor = new AsyncLocalOperationContextAccessor();
        var outer = Context("function:1");

        using var outerScope = accessor.BeginScope(outer);

        using (accessor.BeginScope(Context("job:a/step:b")))
        {
            Assert.Equal("job:a/step:b", accessor.Current!.IdempotencyScope);
        }

        Assert.Equal(outer, accessor.Current);
    }

    [Fact]
    public void Current_ReturnsNull_ForAScopeDisposedTwice()
    {
        var accessor = new AsyncLocalOperationContextAccessor();
        var scope = accessor.BeginScope(Context("scope-a"));

        scope.Dispose();
        scope.Dispose();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task Current_FlowsToAwaitedWork_ForAScopeOpenedBeforeIt()
    {
        // The whole reason for AsyncLocal. ConsumptionWriter reads this several awaits below the
        // host code that opened the scope, with nothing in between carrying it as a parameter.
        var accessor = new AsyncLocalOperationContextAccessor();
        var context = Context("scope-a");

        using var scope = accessor.BeginScope(context);

        var observed = await Task.Run(async () =>
        {
            await Task.Yield();
            return accessor.Current;
        });

        Assert.Equal(context, observed);
    }

    [Fact]
    public void BeginScope_Throws_ForANullContext()
    {
        var accessor = new AsyncLocalOperationContextAccessor();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = accessor.BeginScope(null!);
        });
    }
}
