namespace Corely.Billing.Usage;

internal sealed class UsageVocabulary(
    IReadOnlyList<UsageOperationDefinition> operations,
    IReadOnlyList<UsageUnitDefinition> units
) : IUsageVocabulary
{
    private readonly Dictionary<UsageOperation, string> _operations = operations.ToDictionary(
        definition => definition.Operation,
        definition => definition.DisplayName
    );
    private readonly Dictionary<UsageUnit, string> _units = units.ToDictionary(
        definition => definition.Unit,
        definition => definition.DisplayName
    );

    public IReadOnlyList<UsageOperationDefinition> Operations { get; } = operations;
    public IReadOnlyList<UsageUnitDefinition> Units { get; } = units;

    public bool Knows(UsageOperation operation) => _operations.ContainsKey(operation);

    public bool Knows(UsageUnit unit) => _units.ContainsKey(unit);

    public string DisplayName(UsageOperation operation) =>
        _operations.TryGetValue(operation, out var displayName) ? displayName : operation.Value;

    public string DisplayName(UsageUnit unit) =>
        _units.TryGetValue(unit, out var displayName) ? displayName : unit.Value;
}
