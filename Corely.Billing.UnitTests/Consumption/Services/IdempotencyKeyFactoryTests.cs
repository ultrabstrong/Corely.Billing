using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;

namespace Corely.Billing.UnitTests.Consumption.Services;

public class IdempotencyKeyFactoryTests
{
    private static readonly Guid GrantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static OperationContext Context(string scope) => new(Guid.CreateVersion7(), scope);

    [Fact]
    public void Create_ReturnsTheSameKey_ForTheSameScope()
    {
        // The reason the whole design exists. A retry rebuilds this string and the database rejects
        // the second write instead of billing the work twice.
        var first = Create(Context("job:a/step:b"), GrantA);
        var second = Create(Context("job:a/step:b"), GrantA);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_ReturnsDistinctKeys_ForDifferentScopes()
    {
        Assert.NotEqual(
            Create(Context("job:a/step:b"), GrantA),
            Create(Context("job:a/step:c"), GrantA)
        );
    }

    [Fact]
    public void Create_ReturnsDistinctKeys_ForDifferentGrants()
    {
        // One unit of work spanning two grants writes one row per grant. Without the grant in the
        // key those two rows would collide and only the first would be recorded.
        Assert.NotEqual(
            Create(Context("job:a/step:b"), GrantA),
            Create(Context("job:a/step:b"), GrantB)
        );
    }

    [Fact]
    public void Create_ReturnsDistinctKeys_ForDifferentUnits()
    {
        var context = Context("job:a/step:b");

        Assert.NotEqual(
            IdempotencyKeyFactory.Create(context, TestUsage.Extraction, TestUsage.Page, GrantA),
            IdempotencyKeyFactory.Create(context, TestUsage.Extraction, TestUsage.OtherUnit, GrantA)
        );
    }

    [Fact]
    public void Create_ReturnsAReadableKey_ForATypicalScope()
    {
        // Composed rather than hashed, because this column is read by a person during a billing
        // dispute. Sixty-four hex characters would answer nothing.
        Assert.Equal(
            $"job:a/step:b|{TestUsage.Extraction}|{TestUsage.Page}|{GrantA:N}",
            Create(Context("job:a/step:b"), GrantA)
        );
    }

    [Fact]
    public void Create_Throws_ForAScopeTooLongToStore()
    {
        // Truncating would merge two distinct units of work into one key, which is a double-charge
        // in the other direction: the second one silently never gets billed.
        var context = Context(new string('x', ConsumptionEvent.IDEMPOTENCY_KEY_MAX_LENGTH));

        Assert.Throws<ArgumentException>(() => Create(context, GrantA));
    }

    private static string Create(OperationContext context, Guid grantId) =>
        IdempotencyKeyFactory.Create(context, TestUsage.Extraction, TestUsage.Page, grantId);
}
