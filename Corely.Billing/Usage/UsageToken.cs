using System.Diagnostics.CodeAnalysis;

namespace Corely.Billing.Usage;

internal static class UsageToken
{
    internal const int MAX_LENGTH = 100;

    internal static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MAX_LENGTH
        && value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_');

    internal static string Validated(string value, string tokenType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MAX_LENGTH)
        {
            throw new ArgumentException(
                $"A {tokenType} is at most {MAX_LENGTH} characters: '{value}'.",
                nameof(value)
            );
        }

        foreach (var character in value)
        {
            if (
                !char.IsAsciiLetterLower(character)
                && !char.IsAsciiDigit(character)
                && character != '_'
            )
            {
                throw new ArgumentException(
                    $"A {tokenType} is lowercase letters, digits and underscores: '{value}'.",
                    nameof(value)
                );
            }
        }

        return value;
    }
}
