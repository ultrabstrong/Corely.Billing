namespace Corely.Billing.Usage;

public interface IUsageVocabulary
{
    IReadOnlyList<UsageOperationDefinition> Operations { get; }
    IReadOnlyList<UsageUnitDefinition> Units { get; }

    bool Knows(UsageOperation operation);
    bool Knows(UsageUnit unit);

    UsageOperationDefinition? Definition(UsageOperation operation);
    UsageUnitDefinition? Definition(UsageUnit unit);

    bool Allows(UsageUnit? unit, decimal? quantity);

    string DisplayName(UsageOperation operation);
    string DisplayName(UsageUnit unit);
}
