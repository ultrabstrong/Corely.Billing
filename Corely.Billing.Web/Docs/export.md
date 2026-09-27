# Export

The raw usage behind the components, as CSV, for analysis anywhere else. The dashboard's **Export** downloads a zip of all of it; `ConsumptionTable`, `GrantList` and `UsageChart` each download their own file.

| File | From | One row per |
|---|---|---|
| `usage-events.csv` | `ConsumptionTable`, the dashboard | Ledger row matching the filter, holds and released rows included |
| `grants.csv` | `GrantList`, the dashboard | Grant of the account |
| `chart-series.csv` | `UsageChart`, the dashboard | Period and series, as the chart draws them |
| `README.md` | The dashboard | What every file and column means, the filter, and whether the export was capped |

## usage-events.csv

| Column | Meaning |
|---|---|
| `consumption_id` | The ledger row |
| `occurred_utc` | When the row was written |
| `account_id` | The account charged |
| `operation`, `operation_name` | The stored token, and its display name |
| `unit`, `unit_name` | The stored token, and its display name |
| `quantity` | What the row charged, or holds |
| `status` | `settled`, `held`, `held_expired` (older than the reservation TTL, never resolved) or `released` |
| `counts_toward_balance` | `true` for settled rows and holds within the TTL; summing `quantity` over these gives what a grant has used |
| `finalized_utc` | When a hold was settled or released |
| `provider` | Who did the work |
| `grant_id` | The grant the row drew on; joins to `grants.csv` |
| `work_id` | The operation scope the row was charged under. One charge split across grants is several rows with one `work_id` |
| `correlation_id` | Joins to the host's logs |
| `user_id` | Who asked, when the host passed it |
| `tags` | A JSON object |

## grants.csv

| Column | Meaning |
|---|---|
| `grant_id` | The grant |
| `operation`, `operation_name`, `unit`, `unit_name` | What it covers |
| `quantity` | The allowance; empty when unlimited |
| `unlimited` | `true` for a null quantity |
| `valid_from_utc`, `valid_to_utc` | The window |
| `status` | `upcoming`, `active` or `expired` when exported |
| `used` | Everything counted against the grant when exported |
| `remaining` | Never below zero; empty when unlimited |
| `overdrawn_by` | How far `used` went past `quantity` |
| `tags` | A JSON object |

## Format

- RFC 4180, UTF-8 with a byte order mark so Excel reads it, invariant culture numbers, ISO 8601 UTC times
- A cell that a spreadsheet would run as a formula (starting `=`, `+`, `-`, `@`, a tab or a carriage return) gets a leading `'`, since providers and tags are caller-supplied text
- Tokens and display names side by side: tokens for joins and re-import, names for people

## Notes

- **Capped at 100,000 events**, newest first. A capped export still downloads; the component then says how many of how many events it holds, and the README says the same.
- Reads go through the same services as the components, so a host's authorization decorators apply: what a caller may not read, it does not get.
- The file is built on the server and handed to the browser through `billing-download.js`, served from `_content/Corely.Billing.Web/`. Nothing is inline, so it runs under a strict Content Security Policy.
