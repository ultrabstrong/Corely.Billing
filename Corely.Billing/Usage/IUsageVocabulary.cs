namespace Corely.Billing.Usage;

public interface IUsageVocabulary
{
    IReadOnlyList<UsageOperationDefinition> Operations { get; }
    IReadOnlyList<UsageUnitDefinition> Units { get; }

    bool Knows(UsageOperation operation);
    bool Knows(UsageUnit unit);

    UsageOperationDefinition? Definition(UsageOperation operation);

    string DisplayName(UsageOperation operation);
    string DisplayName(UsageUnit unit);
}
