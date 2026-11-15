# Migrating to Corely.Billing 7.0

7.0 prices consumable usage. A consumable operation can have a price per unit, a grant keeps the
price it was created at, and the components show money beside every priced amount. An operation
with no price behaves exactly as before.

## What breaks

1. **The database has a new table and two new columns.** Apply the migration before starting a
   host on 7.0 (below).
2. **`CreateGrantRequest` and `UpdateGrantRequest` take a last, optional `UnitPrice`.** Source that
   builds them compiles unchanged; a binary built against 6.0 does not load.
3. **`IUsagePriceService` is a fifth service.** A host that decorates every service, or checks
   coverage of every service method, accounts for it: its methods are platform wide and take no
   account.
4. **Startup validates `PriceOptions:Prices`.** A price for an operation that is not a registered
   consumable, an invalid price, or any configured price on a host that calls `UsePlatformPrices()`
   fails `AddBillingServices`.

## Choosing where prices come from

A host without a platform owner sets prices in configuration (`PriceOptions:Prices`). A host with
one calls `UsePlatformPrices()`, and the owner sets them on a page hosting `PriceSettings`. Nothing
is priced until one of the two is done.

## The database

The migration `UsagePrices` adds `UnitPriceAmount` (`decimal(19,6)`) and `UnitPriceCurrency` to
`Grants`, both nullable, and creates `UsagePrices`. Existing grants have no price, so they show no
money. Apply it with the `corely-billing-db` tool 7.0, or the script it generates.

## What follows the major

`Corely.Billing.IAM` 8.0 adds the `usage_price` resource type and decorates `IUsagePriceService`:
anyone reads a price, and only a holder of `usage_price` in the platform account, or system
context, sets or clears one. `Corely.Billing.Web` 9.0 adds `PriceSettings` and
money in the request form, editor, grant list and consumption table. `Corely.Billing.Web.IAM` 11.0,
the compiled models 7.0 and the `corely-billing-db` tool 7.0 take Corely.Billing 7.0.
