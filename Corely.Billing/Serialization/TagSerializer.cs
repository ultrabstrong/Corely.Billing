using System.Text.Json;

namespace Corely.Billing.Serialization;

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
