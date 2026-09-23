namespace Corely.Billing.Operations;

public interface IOperationContextAccessor
{
    OperationContext? Current { get; }

    IDisposable BeginScope(OperationContext context);
}
