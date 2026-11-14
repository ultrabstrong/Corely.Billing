namespace Corely.Billing.Usage;

internal sealed class UsageVocabulary(
    IReadOnlyList<UsageOperationDefinition> operations,
    IReadOnlyList<UsageUnitDefinition> units
) : IUsageVocabulary
{
    private readonly Dictionary<UsageOperation, UsageOperationDefinition> _operations =
        operations.ToDictionary(definition => definition.Operation);
    private readonly Dictionary<UsageUnit, UsageUnitDefinition> _units = units.ToDictionary(
        definition => definition.Unit
    );

    public IReadOnlyList<UsageOperationDefinition> Operations { get; } = operations;
    public IReadOnlyList<UsageUnitDefinition> Units { get; } = units;

    public bool Knows(UsageOperation operation) => _operations.ContainsKey(operation);

    public bool Knows(UsageUnit unit) => _units.ContainsKey(unit);

    public UsageOperationDefinition? Definition(UsageOperation operation) =>
        _operations.GetValueOrDefault(operation);

    public UsageUnitDefinition? Definition(UsageUnit unit) => _units.GetValueOrDefault(unit);

    public bool Allows(UsageUnit? unit, decimal? quantity) =>
        quantity is not { } q || unit is not { } u || Definition(u)?.Allows(q) != false;

    public string DisplayName(UsageOperation operation) =>
        _operations.TryGetValue(operation, out var definition)
            ? definition.DisplayName
            : operation.Value;

    public string DisplayName(UsageUnit unit) =>
        _units.TryGetValue(unit, out var definition) ? definition.DisplayName : unit.Value;
}
