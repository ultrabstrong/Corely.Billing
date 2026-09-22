namespace Corely.Billing;

public sealed class UsageVocabularyBuilder
{
    private readonly List<UsageOperationDefinition> _operations = [];
    private readonly List<UsageUnitDefinition> _units = [];

    public UsageVocabularyBuilder Operation(string value, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var operation = UsageOperation.From(value);

        if (_operations.Any(definition => definition.Operation == operation))
        {
            throw new ArgumentException(
                $"Operation '{value}' is already registered.",
                nameof(value)
            );
        }

        _operations.Add(new UsageOperationDefinition(operation, displayName));
        return this;
    }

    public UsageVocabularyBuilder Unit(string value, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var unit = UsageUnit.From(value);

        if (_units.Any(definition => definition.Unit == unit))
        {
            throw new ArgumentException($"Unit '{value}' is already registered.", nameof(value));
        }

        _units.Add(new UsageUnitDefinition(unit, displayName));
        return this;
    }

    public IUsageVocabulary Build()
    {
        if (_operations.Count == 0 || _units.Count == 0)
        {
            throw new InvalidOperationException(
                "A usage vocabulary needs at least one operation and one unit. Nothing can be "
                    + "granted or consumed without them."
            );
        }

        return new UsageVocabulary(_operations, _units);
    }
}
