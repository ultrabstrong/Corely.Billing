namespace Corely.Billing.Operations;

internal sealed class AsyncLocalOperationContextAccessor : IOperationContextAccessor
{
    // Instance, not static: separate accessors must not share a slot.
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
