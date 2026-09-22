namespace Corely.Billing;

/// <summary>
/// Flows the operation context down the async call chain.
/// </summary>
/// <remarks>
/// This is an implementation living in a business project, which the layering rule in CLAUDE.md
/// normally forbids. It stays because <see cref="AsyncLocal{T}"/> is the BCL's own answer to ambient
/// state and not a vendor's: nothing about this type couples the domain to a logging, hosting or
/// storage choice, and copying it into three hosts would be duplication with no boundary bought.
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
