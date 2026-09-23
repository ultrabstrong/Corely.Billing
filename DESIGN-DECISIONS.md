# Design Decisions

Ideas that were dropped but are easy to remember as done. Open ideas live in
`Plans/Feature-Ideas.md`.

## Host-owned migrations

Ship only entity types and configurations, and let each host generate and keep the billing
migrations in its own migration history.

**Why not.** Every new host would have to regenerate and maintain the same schema, and each copy
would drift. Corely.IAM ships its migrations in a CLI tool for exactly this reason, and billing does
the same: `corely-billing-db` owns the schema, in its own history table, so it shares a database
with IAM and the host without either touching the other's migrations.

**Revisit if** a host needs to change the billing tables themselves. That is a fork of the schema,
not a different place to keep it.

## Distinct names for result types shared with other Corely libraries

Corely.IAM defines `RetrieveResultCode`, `ModifyResultCode`, `ModifyResult`, `PagedResult<T>`,
`RetrieveSingleResult<T>` and `RetrieveListResult<T>`, the same names as `Corely.Billing.Models`.
Rename Billing's set (`BillingRetrieveResultCode`), give both a library prefix, or move them into
Corely.Common, so a file importing both needs no alias.

**Why not.**

- **A prefix on only the colliding types leaks.** The prefix would record a fact about a sibling
  library, not about the type, and would sit beside unprefixed domain types (`Grant`,
  `ConsumptionEvent`). Each new library would rerun the rule against whatever it happens to collide
  with.
- **The types that collide are the generic ones, and that is expected.** Domain types never collide,
  because their names are specific. Generic plumbing does, which is what namespaces are for.
- **Sharing them from Corely.Common couples the libraries through their most frequently changed
  types.** The two sets have already diverged: IAM's `ModifyResultCode` has codes Billing's lacks,
  and IAM's `RetrieveSingleResult<T>` carries effective permissions. Each library needs to change
  its own without a release of the other.
- **Naming by operation (`ListGrantsResult`) multiplies types** to fix a collision seen in one file.

A file that imports both namespaces uses an alias, e.g.
`using BillingRetrieveResultCode = Corely.Billing.Models.RetrieveResultCode;`.

**Revisit if** the collision spreads well beyond a few host files, or a third library makes aliasing
routine.
