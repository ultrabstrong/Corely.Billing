namespace Corely.Billing.Operations;

/// <summary>
/// Flows the operation context down the async call chain.
/// </summary>
/// <remarks>
/// <see cref="AsyncLocal{T}"/> rather than a scoped service, because the scope a host opens for a
/// unit of work is not always a DI scope: a background loop or a message handler can run many units
/// of work inside one. The context follows the async flow, not the container.
/// </remarks>
internal sealed class AsyncLocalOperationContextAccessor : IOperationContextAccessor
{
    // Instance state, not static. The DI registration is a singleton so there is one of these per
    // host anyway, and an instance field keeps two accessors -- two tests running in parallel, say --
    // from silently sharing one ambient slot.
    private readonly AsyncLocal<OperationContext?> _current = new();

    public OperationContext? Current => _current.Value;

    public IDisposable BeginScope(OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = _current.Value;
        _current.Value = context;
        return new Scope(this, previous);
    }

    private sealed class Scope(AsyncLocalOperationContextAccessor owner, OperationContext? previous)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            owner._current.Value = previous;
        }
    }
}
