namespace Corely.Billing.Usage;

public readonly record struct UsageOperation : IComparable<UsageOperation>
{
    public const int MAX_LENGTH = UsageToken.MAX_LENGTH;

    private readonly string? _value;

    private UsageOperation(string value) => _value = value;

    public string Value => _value ?? string.Empty;

    public static UsageOperation From(string value) =>
        new(UsageToken.Validated(value, nameof(UsageOperation)));

    public static bool TryFrom(string? value, out UsageOperation result)
    {
        result = UsageToken.IsValid(value) ? new(value) : default;
        return UsageToken.IsValid(value);
    }

    public int CompareTo(UsageOperation other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;
}
