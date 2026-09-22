namespace Corely.Billing.Usage;

public interface IUsageVocabulary
{
    IReadOnlyList<UsageOperationDefinition> Operations { get; }
    IReadOnlyList<UsageUnitDefinition> Units { get; }

    bool Knows(UsageOperation operation);
    bool Knows(UsageUnit unit);

    string DisplayName(UsageOperation operation);
    string DisplayName(UsageUnit unit);
}
