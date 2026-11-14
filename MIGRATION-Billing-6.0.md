# Migrating to Corely.Billing 6.0

6.0 counts in decimals. A unit declares how many decimal places it allows when it is registered,
so a host can charge a fraction of a unit (4.5 credits) without rounding, while units that are
counted whole stay whole.

## What breaks

1. **Every amount is a `decimal`.** `Quantity`, `Used`, `Remaining` and `InUse` on `Grant`; the
   quantities on `CreateGrantRequest`, `UpdateGrantRequest`, `RequestGrantRequest`, `GrantRequest`,
   `ConsumptionEvent`, `ReserveQuotaRequest` and `GrantShare`; `SettleQuotaRequest.ActualQuantity`;
   `SettledQuantity` on the settle result; `Remaining`, `Total`, `Granted`, `InUse` and `Free` on the
   quota reads; `IConsumptionService.GetConsumptionTotalAsync`; and the consumption series and their
   buckets. A host's own code that held these in a `long` holds them in a `decimal`. Whole numbers
   convert implicitly, so most call sites that pass a literal compile unchanged.
2. **`RegisterUnit` takes the unit's decimal places,** `RegisterUnit(value, displayName, decimals)`.
   It defaults to 0, so an existing registration keeps counting whole units. At most 6.
3. **A quantity finer than its unit allows is refused,** with `ValidationError`, never rounded: on
   reserve, on settle, on a grant and on a grant request. 1.5 of a whole unit is refused, and so is
   0.0000001 of a unit registered with 6 places.
4. **`SettleQuotaResultCode` has a new value,** `ValidationError`, for a settlement whose quantity
   is negative or finer than its unit. A `switch` over the enum handles it. The bearer API answers
   it with 400.
5. **`IUsageVocabulary` has two new members,** `Definition(UsageUnit)` and
   `Allows(UsageUnit?, decimal?)`. A host that implements the interface adds them.
   `UsageUnitDefinition` gains `Decimals` and `Allows(decimal)`.

## The database

The migration `DecimalQuantities` changes the `Quantity` column of `Grants`, `GrantRequests` and
`ConsumptionEvents` from `bigint` to `decimal(19,6)`. Every existing whole number is kept as it is.
Apply it with the `corely-billing-db` tool 6.0, or the script it generates, before starting a host
on 6.0.

## What follows the major

`Corely.Billing.IAM` 7.0, `Corely.Billing.Web` 8.0, `Corely.Billing.Web.IAM` 10.0, the compiled
models 6.0 and the `corely-billing-db` tool 6.0 take Corely.Billing 6.0. `Corely.Billing.Web` shows
every amount with the places it has and no trailing zeros (`4.5 credits`, `0.000001 credits`), and its
quantity inputs accept decimals. The bearer API's amounts are JSON numbers that may carry a
fraction.
