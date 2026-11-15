# Grant prices

Released: Corely.Billing 7.0.0, compiled models and corely-billing-db 7.0.0, Corely.Billing.IAM 8.0.0,
Corely.Billing.Web 9.0.0, Corely.Billing.Web.IAM 11.0.0.

Every amount Corely.Billing shows is a count: 20,000 credits, 1,162 pages. Nothing says what it is
worth, so a host that sells by the unit has to bolt money on beside every component, and the
request form and grant editor, where an amount is chosen, have nowhere to put it. DocsToData's
Request a Grant page asks for "2000000 credits" and never says that is $2,000.

This gives a consumable operation an optional price per unit, carried onto each grant when it is
created, and shown by every component wherever a priced amount appears.

## Decisions

1. **A price is optional and per unit of a consumable operation.** An operation registered with no
   price shows no money anywhere. One priced operation for everything (DocsToData's credits) and a
   price per operation are the same feature.
2. **Only consumable grants are priced.** Their amount is a number of units, and usage spends them,
   so units times price is what was bought and what was spent. Capacity, allowance and feature
   grants are not sold by the unit: a seat or a retention day is worth something only per period,
   and a feature is a flat price. Those are subscription prices, a different shape, and are left out
   rather than forced into a per unit price that would read wrong.
3. **The grant keeps the price it was created at.** Creating a grant copies the operation's current
   unit price onto it; the editor may change it, for a negotiated price. Usage and balances are
   valued at their own grant's price, so a past charge always shows what it cost, whatever the
   price is today.
4. **The current price is configurable, from one of two places.** Without a platform account it is
   read from configuration (`BillingOptions:Prices`, keyed by operation). With one, it is the
   platform's price setting the owner edits, and configuration is ignored. The two never layer,
   and the registration carries no price, so code is never a hidden default. This is the rule
   Corely.IAM's security settings follow. Because of 3, a change affects only grants created
   after it.
5. **Components show money whenever a price exists.** No per component flag. The request form,
   grant editor, grants list, balance and usage views show the dollar value beside the count. One
   library option turns it off for a host that wants counts only.
6. **The host's own pricing stays the host's.** DocsToData's rate sheet turns provider work into
   credits; this turns credits into money. Neither knows about the other.

## Corely.Billing

1. **Money type.** `Money(decimal Amount, string Currency)`, ISO 4217, `USD` the only currency
   offered for now. Formatting exact, trailing zeros trimmed, nothing rounded, as DocsToData's
   `Credits` already does. Unit prices take up to 6 decimal places, enough for $0.001.
2. **Configured prices.** `BillingOptions:Prices`, operation to unit price and currency, validated
   at startup: a price for an operation that is not registered, or not consumable, fails with the
   problem named.
3. **Where a price comes from.** `IUsagePriceService`: get the current price for an operation, or
   none. Core reads configuration. A `UsagePrices` table, one row per operation with who set it
   when, backs the platform source, which Corely.Billing.IAM switches on when Corely.IAM has a
   platform account; set and clear exist only there and answer with a result code saying so
   without one.
4. **Grant price.** `Grants.UnitPrice` and `Grants.Currency`, nullable. Set on create from the
   current price unless the request names one; editable on update. Null for unpriced and non
   consumable grants. A request naming a price for a non consumable grant is a `ValidationError`.
5. **Valuing.** `Grant.Value()` (quantity times price), and a consumption's value at its grant's
   price. Balance and usage queries return the money beside the count, summed per currency.
6. **Migration** for both providers; existing grants get no price (null), which reads as unpriced.

## Corely.Billing.IAM

The platform price source: with a platform account, prices come from the `UsagePrices` rows and
configuration is ignored; startup logs a warning if `BillingOptions:Prices` is set anyway, as
Corely.IAM does for security settings. A `usage_price` resource type in the platform account:
the owner gets CRUD, everyone else nothing. The authorization decorator on `IUsagePriceService`,
and the coverage tests the other services have.

## Corely.Billing.Web

1. **Request form and grant editor** show "= $2,000.00" under the quantity as it is typed, from the
   operation's current price. The editor also shows and edits the grant's unit price.
2. **Grants list, balances, usage and charts** show each priced amount's value at its grant's price.
3. **A price settings component** for a host's platform settings page: each consumable operation,
   its registered price, the override, set and clear.
4. **The approve and decline dialog sits low on the page.** It is `modal-dialog-centered`, yet it
   renders well below the viewport's centre on DocsToData's Grants page, which points at an ancestor
   that makes a fixed element position against it rather than the viewport (a transform, filter or
   containment). Find the cause in the browser and fix it in the component, so no host has to
   avoid it. While there: "Asked for" reads "20,000 credits, Credits", naming the operation twice.

## Release

Majors, since the grant model and the database change: Corely.Billing, its compiled models and
database package, Billing.IAM, Billing.Web, Billing.Web.IAM. A `MIGRATION-Billing-7.0.md` says what
breaks. Released in the usual order, one tag at a time.

## DocsToData

1. DocsToData has a platform account, so the credit's price is a platform setting, set on the
   existing **Rates** tab above the rate sheet (no new page): the price settings component sits
   there, and the owner sets $0.001. Until it is set, credits are unpriced and nothing shows money.
2. **`Credits`' fixed $0.001 goes.** A credit's dollar value is no longer a constant, so every
   place DocsToData shows dollars (the job page, Run Once, the dashboard, Rates, Getting Started,
   grant request emails, the API's workflow check) reads the current price through
   `IUsagePriceService`, or the grant's price for a past charge, and says nothing in dollars when
   none is set. The rate sheet stays in credits.
3. Drop the portal's own credits to dollars text where the library now shows it, so a value is
   computed in one place.
4. Click through locally on desktop and phone: request, approve, create, grants list, usage.

## Tests

1. **Unit:** money formatting; a price refused for a non consumable registration; the current price
   resolves override, then registration, then none; a grant takes the current price on create and
   keeps it after the price changes; the request form and editor show the value as typed.
2. **Integration:** the migration; a grant's value and a balance's money read through SQL; a price
   change leaves an existing grant's value alone (the invariant an invoice would state).
3. **Coverage:** `IUsagePriceService` in the authorization and audit coverage tests.

## As built

- **Where a price comes from is the host's call, not a runtime check.** Corely.Billing cannot see
  whether Corely.IAM has a platform account, so a host with a platform owner calls
  `UsePlatformPrices()`. Configured prices on such a host fail startup rather than log a warning,
  since nothing depends on them yet.
- **No option to turn money off.** A host that sets no price shows none, which is the same thing.
- **Money formats itself.** `Money.ToString()` writes every digit with at least cents, so a host's
  emails and the components read the same text; there is no Billing.Web extension for it.
- **Balance and usage readings are members, not query fields.** `Grant.Value()`, `Grant.Price()`
  and `GetBalanceResult.RemainingValue()`; the consumption table values each event at its grant's
  price. The charts stay in counts.
- **The dialog.** Corely.IAM.Web gives every `.modal-dialog` a 15vh top margin; with
  `modal-dialog-centered` that pushed the screen tall dialog down. The dialog dropped the centering
  and now sits where every other modal in a host using Corely.IAM.Web sits.
- **`PriceSettings` has `OnChanged`,** so a host page can show the new price elsewhere.
