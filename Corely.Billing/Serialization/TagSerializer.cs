using System.Text.Json;

namespace Corely.Billing.Serialization;

/// <summary>
/// Reads and writes the tag dictionaries that grants and consumption rows persist as JSON.
/// </summary>
/// <remarks>
/// One home for both directions. Copying the <see cref="JsonSerializerOptions"/> into each mapper
/// would let them drift, and a drift there corrupts tags silently rather than failing.
/// </remarks>
internal static class TagSerializer
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);

    public static string? Serialize(IReadOnlyDictionary<string, string>? tags) =>
        tags is null ? null : JsonSerializer.Serialize(tags, s_options);

    public static Dictionary<string, string>? Deserialize(string? tagsJson) =>
        string.IsNullOrWhiteSpace(tagsJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson, s_options);
}
