using System.Text.Json;

namespace Corely.Billing;

/// <summary>
/// Reads and writes the tag dictionaries that <c>Grant</c> and <c>ConsumptionEvent</c> persist as
/// JSON strings.
/// </summary>
/// <remarks>
/// The two halves of the round trip live in different places: serialization stays in the mapper,
/// which owns the write direction, while deserialization moved onto the domain model along with
/// <c>FromEntity</c>. Copying the <see cref="JsonSerializerOptions"/> to both sides would let them
/// drift, and a drift there corrupts tags silently rather than failing. One home instead.
/// </remarks>
public static class TagSerializer
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);

    public static string? Serialize(IReadOnlyDictionary<string, string>? tags) =>
        tags is null ? null : JsonSerializer.Serialize(tags, s_options);

    public static IReadOnlyDictionary<string, string>? Deserialize(string? tagsJson) =>
        string.IsNullOrWhiteSpace(tagsJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson, s_options);
}
