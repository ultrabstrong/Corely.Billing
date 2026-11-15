# IUsagePriceService

What one unit of a consumable operation costs, for grants created now. A price is optional: an
operation with none shows no money anywhere. Only a consumable operation has a price per unit;
capacity, allowance and feature grants are not sold by the unit.

## Methods

| Member | Parameters | Returns |
|--------|-----------|---------|
| `Source` | | `PriceSource`: `Configuration` or `Platform` |
| `GetPriceAsync` | `UsageOperation` | `Money?` |
| `ListPricesAsync` | | `IReadOnlyList<UsagePrice>` |
| `SetPriceAsync` | `UsagePrice` | `ChangeUsagePriceResult` |
| `ClearPriceAsync` | `UsageOperation` | `ChangeUsagePriceResult` |

## Where a price comes from

One place, never both:

| The host | Prices come from | Set and clear |
|----------|------------------|---------------|
| Calls nothing | Configuration: `PriceOptions:Prices` | `ConfiguredError` |
| Calls `UsePlatformPrices()` | The `UsagePrices` table, which the platform owner edits | Change the table |

A host with a platform owner calls `UsePlatformPrices()`; one without configures prices:

```json
{
  "PriceOptions": {
    "Prices": {
      "credits": { "Amount": 0.001, "Currency": "USD" }
    }
  }
}
```

Startup refuses a configured price for an operation that is not a registered consumable, a price
that is negative or finer than 6 decimal places, and configured prices on a host that called
`UsePlatformPrices()`, where they would be ignored.

## A grant keeps its price

Creating a consumable grant copies the operation's current price onto it as `Grant.UnitPrice`,
unless the request names one (a negotiated price). Changing the price later leaves every existing
grant's price alone, so a grant's value and every charge against it read what was actually paid:

```csharp
grant.Value();               // Quantity times UnitPrice, or null when unpriced
grant.Price(4.5m);           // what 4.5 units cost at this grant's price
```

An update that names no price keeps the grant's own.

## Money

`Money(decimal Amount, string Currency)`, with `Money.Usd(amount)`. Amounts are exact: `Times`
keeps every digit, and nothing rounds. `ToString()` writes every digit with at least cents
(`$2,000.00`, `$0.0045`). `Problem()` says what makes a price invalid, or null.

`GetBalanceResult.RemainingValue()` is what is left across the balance's grants, each at its own
price, or null when any grant with something left is unpriced.
