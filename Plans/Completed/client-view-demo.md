# Client view demo

**Status: done.** Released as Corely.Billing.Web 2.4.0.

## As built

- The client is `chloe`, not `cleo`. Corely.IAM requires usernames of at least five characters, so
  the seed could not register `cleo`.
- `UsageChart` and `ConsumptionTable` each take a `ShowGrants` parameter, set by the dashboard.
  `UsageChartView.ReadsGrants()` and `UsageBreakdown.ReadsGrants()` say which views and breakdowns
  are hidden, tested directly. A `View` or `BreakDownBy` that needs grants opens as Used, by operation.
- `UsageExporter.DownloadAllAsync` takes `includeGrants` from the dashboard rather than reading the
  grant list again.
- `grant_id` stays in `usage-events.csv` for a client; it is part of the consumption a client may read.
- `Start-LocalStack.ps1` still installed `corely-iam-db` 3.x after the move to IAM 4, so the WithIAM
  seed failed on a missing table. It installs 4.x now.

The WithIAM demo shows an owner, a grant editor and a member with no roles. It has no client: the
customer of a metered product, who sees what they used and nothing about the grants behind it. This
plan adds one, `cleo`, to the existing demo rather than a new demo.

## Model after `carla`

`carla` is the precedent for every part of this. Copy her shape, do not invent a new one:

| Part | `carla` today | `cleo` |
|---|---|---|
| Seed | `DemoSeed.RegisterGrantEditorAsync`: a role built under `AuthenticateAsSystem`, reusing the owner's system defined `consumption` permission by lookup | `RegisterClientAsync`, same structure: a "Client" role holding the owner's system defined `consumption` and `quota` permissions, no `grant` permission |
| User | `RegisterUserAsync` then `RegisterUserWithAccountAsync` as `olivia` | Same, alongside `bobby` and `carla` |
| README | A row in the users table and a paragraph saying what she proves | A row and a paragraph: usage with no trace of grants |
| Console | Named in the seed's closing `Console.WriteLine` | Added there, "Password for all four" |

`cleo` holds Execute on `quota` so "Ask the assistant" works for her: a client generates usage.

## Library: a usage view without grant access

Today `cleo` would see nothing. `UsageDashboard.LoadReferenceDataAsync` lists grants first and, on
`UnauthorizedError`, sets `_error` to "not allowed to view usage" and stops
(`Corely.Billing.Web/Components/UsageDashboard.razor.cs`, around line 112). Usage is gated on
`consumption`, so failing the whole dashboard on `grant` is the bug to fix.

The rule: when the grant list comes back `UnauthorizedError`, the dashboard runs without grants.
Decide it once in the dashboard from the service result, not from IAM, so `Corely.Billing.Web` stays
free of IAM. `UsageChart.LoadAsync` already treats a failed grant list as no grants; that is the
precedent for tolerating it.

Without grant access:

1. No Grant filter in the toolbar.
2. `_earliest` comes from consumption alone.
3. `UsageChart` offers only the views that need no grants (`Used`, `Stacked`, `SideBySide`, `Share`),
   drops `UsageBreakdown.Grant`, and shows no capacity chart and no "no limited grant covers this
   range" text. Pass this in as a parameter from the dashboard rather than having the chart probe
   again.
4. `ConsumptionTable` hides its Grant column, through a parameter set by the dashboard.
5. Export still works: `UsageExporter.DownloadAllAsync` leaves out the grants file instead of
   failing, and the README inside the zip says the grants were not included.

Users who can read grants see exactly what they see today.

Update `Corely.Billing.Web/Docs/components/` for any new parameter, and add unit tests for the
dashboard, chart and table with the grant list unauthorized, beside the existing ones in
`Corely.Billing.Web.UnitTests`.

## Demo: no grant chrome for a client

1. `DemoLayout.razor`: show the Grants nav link only to users with Read on `grant`. Use Corely.IAM.Web's
   `PermissionView`, as `Corely.Billing.Web.IAM/PermissionViewGrantActionGate.cs` does.
2. `Home.razor` needs no change once the dashboard handles the case.
3. `/grants` typed by hand still shows the library's "not allowed" message. That is correct; a client
   who guesses the URL should be refused, not shown an empty page.

## Done when

1. `.\Start-LocalStack.ps1 -Demo WithIAM`, signed in as `cleo`: usage chart and event table load, no
   Grants link, no Grant filter, no grant column, no grant views, Ask the assistant works, Export
   downloads a zip without the grants file.
2. `olivia` and `carla` see no change.
3. `RebuildAndTest.ps1` passes.
4. `Corely.Billing.Web` is released with the change, since DocsToData consumes it. A user there who
   can read consumption but not grants now gets a working usage page instead of an error.
