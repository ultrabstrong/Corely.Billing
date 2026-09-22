using Corely.Billing;
using Corely.Billing.Grants.DataAccess;

namespace Corely.Billing.Grants.Models;

public sealed record Grant
{
    // Constraints
    internal const int QUANTITY_MIN_VALUE = 0;

    public Guid GrantId { get; }
    public Guid AccountId { get; }
    public long Quantity { get; }
    public UsageUnit Unit { get; }
    public UsageOperation Operation { get; }
    public DateTime ValidFromUtc { get; }
    public DateTime ValidToUtc { get; }
    public IReadOnlyDictionary<string, string>? Tags { get; }

    private Grant(
        Guid grantId,
        Guid accountId,
        long quantity,
        UsageUnit unit,
        UsageOperation operation,
        DateTime validFromUtc,
        DateTime validToUtc,
        IReadOnlyDictionary<string, string>? tags = null
    )
    {
        GrantId = grantId;
        AccountId = accountId;
        Quantity = quantity;
        Unit = unit;
        Operation = operation;
        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
        Tags = tags;
    }

    public static CreateResult<Grant> Create(
        Guid accountId,
        long quantity,
        UsageUnit unit,
        UsageOperation operation,
        DateTime validFromUtc,
        DateTime validToUtc,
        Guid? grantId = null,
        IReadOnlyDictionary<string, string>? tags = null
    )
    {
        var candidate = new Grant(
            grantId ?? Guid.CreateVersion7(),
            accountId,
            quantity,
            unit,
            operation,
            validFromUtc,
            validToUtc,
            tags
        );

        return Invalidity(candidate) is { } invalidity
            ? CreateResult<Grant>.Invalid(invalidity)
            : CreateResult<Grant>.Success(candidate);
    }

    private static string? Invalidity(Grant candidate)
    {
        if (candidate.AccountId == Guid.Empty)
            return $"{nameof(AccountId)} is required.";

        if (candidate.GrantId == Guid.Empty)
            return $"{nameof(GrantId)} is required.";

        if (candidate.Quantity < QUANTITY_MIN_VALUE)
            return $"{nameof(Quantity)} must be {QUANTITY_MIN_VALUE} or greater.";

        return null;
    }

    internal static Grant FromEntity(GrantEntity entity) =>
        new(
            entity.GrantId,
            entity.AccountId,
            entity.Quantity,
            entity.Unit,
            entity.Operation,
            entity.ValidFromUtc,
            entity.ValidToUtc,
            TagSerializer.Deserialize(entity.TagsJson)
        );
}
