# Preface
List of features that may be nice to implement

Dropped ideas that are easy to remember as done are in [`DESIGN-DECISIONS.md`](../DESIGN-DECISIONS.md).

### Grant expiry notifications
- [ ] Report grants whose `ValidToUtc` falls within a window, for renewal reminders
- [ ] Include what remains on each, so an unused grant is visible before it lapses

### Abandoned reservation sweep
Holds past their TTL already stop counting. A sweep would mark them released, so the ledger reads
the same without knowing the TTL rule.

### Per-account reservation TTL
`ReservationTtl` is app-wide. Work with very different run times might want a TTL per operation.
