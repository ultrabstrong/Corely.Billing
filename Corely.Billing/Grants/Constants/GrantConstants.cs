namespace Corely.Billing.Grants.Constants;

public static class GrantConstants
{
    public const int QUANTITY_MIN_VALUE = 0;
    public const int KIND_MAX_LENGTH = 20;
    public static readonly DateTime NEVER_EXPIRES_UTC = new(
        9999,
        12,
        31,
        0,
        0,
        0,
        DateTimeKind.Utc
    );
}
