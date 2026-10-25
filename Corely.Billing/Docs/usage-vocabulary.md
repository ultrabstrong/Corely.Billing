# Usage Vocabulary

Operations and units are host-defined tokens. `UsageOperation` names what is granted (`text_generation`, `seats`, `sftp_access`); `UsageUnit` names what it is counted in (`token`, `user`). Each operation declares its [grant kind](usage-shapes.md) and its unit.

## Features

- **Host-defined**: the library ships no operations or units of its own
- **Declared once**: each operation names its kind and unit on `BillingOptions`, checked at startup
- **A guard, not the source**: a grant stores its own kind and unit; the declaration refuses a grant that disagrees
- **Stored as text**: the token is the column value, readable in any query
- **Display names**: a friendly name for reports, separate from the stored token

## Usage

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterUnit("token", "token")
    .RegisterUnit("user", "user")
    .RegisterUnit("day", "day")
    .RegisterOperation("text_generation", "Text Generation", GrantKind.Consumable, "token")
    .RegisterOperation("seats", "Seats", GrantKind.Capacity, "user")
    .RegisterOperation("log_retention", "Log Retention", GrantKind.Allowance, "day")
    .RegisterOperation("sftp_access", "SFTP Access", GrantKind.Feature);
```

A feature declares no unit; every other kind must, and the unit must be registered. Several operations can share one unit.

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
var definition = vocabulary.Definition(MyUsage.TextGeneration); // Kind, Unit, DisplayName
var label = vocabulary.DisplayName(MyUsage.TextGeneration); // "Text Generation"
```

## Notes

- A write naming an unregistered operation or unit returns `ValidationError` and records nothing
- A grant whose kind or unit disagrees with its operation returns `DeclarationMismatchError`
- Each quota read refuses an operation declared as another kind with `WrongKindError`
- `DisplayName` falls back to the token for a value no longer registered, so historical rows still read
- Renaming a token is a data migration; the stored rows keep the old value
