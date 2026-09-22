namespace Corely.Billing;

/// <summary>
/// Reads and sets the ambient <see cref="OperationContext"/>.
/// </summary>
/// <remarks>
/// The same shape as <c>IHttpContextAccessor</c>, and for the same reason: the context is established
/// once at the edge of a unit of work and read far below it, with nothing in between having to carry
/// it as a parameter.
/// </remarks>
public interface IOperationContextAccessor
{
    OperationContext? Current { get; }

    /// <summary>Establishes <paramref name="context"/> until the returned scope is disposed.</summary>
    IDisposable BeginScope(OperationContext context);
}
