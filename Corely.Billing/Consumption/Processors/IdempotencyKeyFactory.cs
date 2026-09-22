using Corely.Billing.Consumption.Constants;
using Corely.Billing.Operations;
using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Processors;

/// <summary>
/// Derives the key that stops a retry from charging twice.
/// </summary>
/// <remarks>
/// <para>
/// Composed rather than hashed. The column is read by a person during a billing dispute, and
/// "scope|operation|unit|grant" answers "what was this row for?" where sixty-four hex
/// characters answers nothing. Uniqueness comes from the scope, which is already unique per unit of
/// work; hashing would buy only a shorter column.
/// </para>
/// <para>
/// The grant is part of the key because one unit of work spanning two grants writes one row per
/// grant, and those rows must not collide with each other.
/// </para>
/// </remarks>
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

        // Truncation would silently merge two distinct units of work into one key, which is a
        // double-charge in the other direction: the second one never gets billed.
        return key.Length <= ConsumptionConstants.IDEMPOTENCY_KEY_MAX_LENGTH
            ? key
            : throw new ArgumentException(
                $"Idempotency scope '{context.IdempotencyScope}' produces a key of {key.Length} "
                    + $"characters, over the {ConsumptionConstants.IDEMPOTENCY_KEY_MAX_LENGTH} allowed.",
                nameof(context)
            );
    }

    /// <summary>
    /// Everything a unit of work's keys share, whichever grants it ended up drawing on.
    /// </summary>
    /// <remarks>
    /// Settle and release find their rows by this prefix rather than by a handle the caller carried,
    /// which is what keeps callers ignorant of reservations altogether.
    /// </remarks>
    public static string Prefix(OperationContext context, UsageOperation operation, UsageUnit unit)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{context.IdempotencyScope}|{operation}|{unit}|";
    }
}
