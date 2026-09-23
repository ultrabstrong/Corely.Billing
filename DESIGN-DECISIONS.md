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
