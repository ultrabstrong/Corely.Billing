using Corely.Billing.Consumption.Constants;
using Corely.Billing.Operations;
using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Processors;

internal static class IdempotencyKeyFactory
{
    public static string Create(
        OperationContext context,
        UsageOperation operation,
        UsageUnit unit,
        Guid grantId
    )
    {
        var key = Prefix(context, operation, unit) + grantId.ToString("N");

        return key.Length <= ConsumptionConstants.IDEMPOTENCY_KEY_MAX_LENGTH
            ? key
            : throw new ArgumentException(
                $"Idempotency scope '{context.IdempotencyScope}' produces a key of {key.Length} "
                    + $"characters, over the {ConsumptionConstants.IDEMPOTENCY_KEY_MAX_LENGTH} allowed.",
                nameof(context)
            );
    }

    public static string Prefix(OperationContext context, UsageOperation operation, UsageUnit unit)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{context.IdempotencyScope}|{operation}|{unit}|";
    }
}
