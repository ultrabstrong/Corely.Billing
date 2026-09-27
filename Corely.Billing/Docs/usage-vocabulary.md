# Usage Vocabulary

Operations and units are host-defined tokens. `UsageOperation` names what was done (`text_generation`); `UsageUnit` names what it was measured in (`token`). Every grant and consumption row carries one of each.

## Features

- **Host-defined**: the library ships no operations or units of its own
- **Registered once**: declared on `BillingOptions`, validated on every write
- **Stored as text**: the token is the column value, readable in any query
- **Display names**: a friendly name for reports, separate from the stored token

## Usage

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterOperation("text_generation", "Text Generation")
    .RegisterOperation("image_generation", "Image Generation")
    .RegisterUnit("token", "token")
    .RegisterUnit("image", "image");
```

Keep the tokens in one static class so the host never repeats a string:

```csharp
public static class MyUsage
{
    public static UsageOperation TextGeneration { get; } = UsageOperation.From("text_generation");
    public static UsageUnit Token { get; } = UsageUnit.From("token");
}
```

## Token Rules

| Rule | Value |
|------|-------|
| Characters | `a-z`, `0-9`, `_` |
| Maximum length | 100 (`UsageOperation.MAX_LENGTH`, `UsageUnit.MAX_LENGTH`) |
| Invalid token | `From()` throws `ArgumentException` |

## Reading the Vocabulary

```csharp
var vocabulary = serviceProvider.GetRequiredService<IUsageVocabulary>();
var known = vocabulary.Knows(MyUsage.Token);
var label = vocabulary.DisplayName(MyUsage.TextGeneration); // "Text Generation"
```

## Notes

- A write naming an unregistered operation or unit returns `ValidationError` and records nothing
- `DisplayName` falls back to the token for a value no longer registered, so historical rows still read
- Renaming a token is a data migration; the stored rows keep the old value
