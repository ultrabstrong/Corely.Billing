namespace Corely.Billing;

public readonly record struct UsageUnit : IComparable<UsageUnit>
{
    public const int MAX_LENGTH = UsageToken.MAX_LENGTH;

    private readonly string? _value;

    private UsageUnit(string value) => _value = value;

    public string Value => _value ?? string.Empty;

    public static UsageUnit From(string value) =>
        new(UsageToken.Validated(value, nameof(UsageUnit)));

    public int CompareTo(UsageUnit other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;
}
