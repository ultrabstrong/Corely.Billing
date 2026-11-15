# PriceSettings

Each consumable operation's price per unit, for a host's platform settings page. Editable when the
host calls `UsePlatformPrices()` and passes `Editable`; otherwise it shows the prices read only, and
says when they come from configuration.

## Usage

```razor
<PriceSettings Editable="canChangePrices" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `Editable` | `false` | Shows Save and Clear. The service still refuses a caller without the `usage_price` permission in the platform account |
| `OnChanged` | none | Called after a price is set or cleared, so the page can show the new price elsewhere |

## Notes

- Prices are in US dollars, up to 6 decimal places. A finer price is refused, not rounded.
- A price applies to grants created after it is set. A grant keeps the price it was created at.

## Money elsewhere

Every component shows money beside a priced amount, and nothing when there is no price:

| Component | Shows |
|-----------|-------|
| [GrantRequestForm](grant-request-form.md), [GrantEditor](grant-editor.md) | What the quantity being typed is worth, at the current price; the editor also edits the grant's own price |
| [GrantList](grant-list.md) | Each grant's value at its own price; a request's worth at today's price, also in the approve dialog |
| [ConsumptionTable](consumption-table.md) | Each event's cost at its grant's price |

`Money.ToString()` formats an amount exactly, with at least cents:
`$2,000.00`, `$0.0045`.
