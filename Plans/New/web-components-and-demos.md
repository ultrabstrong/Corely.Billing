# Web components and a demo portal

## Starting cold

For a session picking this up with no history.

**Read first:** this repository's `CLAUDE.md`, `DOCUMENTATION-STYLE.md` and `Corely.Billing/Docs/`
(the library's public surface). Corely.IAM is the standard for every convention here; where this
plan is silent, do what IAM does.

**Source to port** (DocsToData, a separate repository, read-only for this work):

| What | Path under `C:\source\git\pinnacleinnovation\DocsToData\DocsToData.AdminPortalWebApp\` |
|---|---|
| Grant list | `Components/Pages/Grants.razor` + `.razor.cs` |
| Grant editor | `Components/Pages/GrantEditor.razor` + `.razor.cs` |
| Usage page | `Components/Pages/Usage.razor` + `.razor.cs` |
| Chart module | `wwwroot/js/usage-chart.js` (Chart.js from `libman.json`, loaded in `Components/App.razor`) |
| Unit counts | `Components/UsageText.cs` |

Those pages already call the current Corely.Billing API, so they port without an API change. Run
DocsToData's local stack (`iac/local/Start-LocalStack.ps1 -Run`, sign in `admin` / `Test1234`) to see
them working.

**The IAM precedent** under `C:\source\git\ultrabstrong\Corely.IAM\`: `Corely.IAM.Web/` (project
shape, `Docs/`, vendored `wwwroot/lib`, `Extensions/` registration), `Corely.IAM.Web.UnitTests/`
(bUnit), `Corely.IAM.Web.FunctionalTests/Demos/` (demo smoke tests and why they need extern
aliases), `Corely.IAM.Demos.*` and `Corely.IAM.Demos.Assets` (demo hosts and shared static assets),
and `Plans/Completed/simple-usage-shapes-docs-and-demos.md` (what went wrong building those demos).

**Ask the owner before building:** the three decisions at the end of this plan. Everything else,
proceed.

**Shipping it:** the new package needs a `dotnet pack` step in `.github/workflows/release.yml`, an
entry in `scripts/check-package-versions.sh`, and its own `<Version>`. The nuget.org trusted-publisher
policy already covers `Corely.Billing*`, so no nuget.org change is needed. Releasing is tagging
`vX.Y.Z` on `master`; ask before tagging, since a published version cannot be deleted.

**Running beside `usage-shapes-docs-and-demos.md`:** that plan changes the library (a migration, a
new service method). Work on a branch or a separate `git worktree`, not the shared checkout. Expect
to merge `Corely.Billing.slnx`, the root `README.md`, `release.yml`, `check-package-versions.sh` and
`Corely.Billing/Docs/index.md`. If that plan makes `Grant.Quantity` nullable, the components print
"Unlimited" for a null quantity; if it lands first, handle it here, and if it lands after, it is that
plan's follow-up.

**Done when:** the package builds, its bUnit and smoke tests pass, `RebuildAndTest.ps1` is green, the
demo portal is clicked through at desktop and phone widths, docs are written, and this plan moves to
`Plans/Completed/` with an Outcome section. Commit locally; push only when the owner says so.

## The problem

Every host of Corely.Billing ends up building the same screens: a list of grants, a form to create
and edit one, and a chart of what an account was given against what it used. DocsToData built all
three in its admin portal (`Grants.razor`, `GrantEditor.razor`, `Usage.razor` with a Chart.js
module). The next host would copy them, and the one after that would copy the copy.

Corely.IAM solved the same problem with `Corely.IAM.Web`: a Razor class library of pages and
components a host drops in. Billing does the same, under the same conventions.

## Deliverables

### 1. `Corely.Billing.Web` — a Razor class library

Packed and published beside `Corely.Billing`, versioned independently in its own csproj, as
`Corely.IAM.Web` is. Blazor Server components, Bootstrap-first styling, scoped CSS.

**No reference to Corely.IAM.** Billing does not require identity, so neither does its UI. Two
things a component needs from the host arrive as abstractions:

- **Which account.** Every component takes an `AccountId` parameter. Routed pages, where there is no
  parent to pass it, read `IBillingAccountAccessor` — one method, `Task<Guid?> GetAccountIdAsync()`,
  that the host implements. An IAM host returns `UserContext.CurrentAccount?.Id`.
- **Who may do what.** Authorization stays in the host's service decorators, where it already is. A
  component shows an `UnauthorizedError` result as a message rather than failing. So a host can hide
  controls rather than only refuse them, the write actions (create, edit, delete) sit behind a
  `RenderFragment` slot or a `CanManage` parameter. An IAM host wraps them in `PermissionView`.

**Components, first as a port of DocsToData's pages to parity:**

| Component | From | Does |
|---|---|---|
| `GrantList` | `Grants.razor` | Grants for an account, with status (upcoming, active, expired), edit and delete. Cards on a phone, a table on a desktop |
| `GrantEditor` | `GrantEditor.razor` | Create and edit. Operation and unit dropdowns from `IUsageVocabulary`; operation fixed once a grant exists |
| `UsageChart` | `Usage.razor` + `usage-chart.js` | Consumption against live grant capacity over time, bucketed day/week/month by range |
| `ConsumptionTable` | `Usage.razor` | Events with the unit, operation, provider and grant filters, sort, paging, and settled/reserved/released badges |
| `UsageDashboard` | `Usage.razor` | The chart, the table and the range presets composed as one drop-in |

**Pages come separately from components.** IAM's lesson (`simple-usage-shapes-docs-and-demos`):
adding the library assembly to the router routes every page it contains, so a host that wanted one
page got all of them. Here the components are the product. A small set of routed pages (`/grants`,
`/grants/new`, `/grants/{id}`, `/usage`) ships too, but a host opts in to routing them, and can compose
its own pages from the components instead.

**Chart.js is vendored in the library** as a static web asset under `wwwroot/lib`, the way IAM.Web
vendors its client libraries, and loaded by the component's own JS module. A host adds nothing to
its layout to make the chart work.

**Registration mirrors IAM.Web:** `services.AddBillingWeb()`, plus the host's
`IBillingAccountAccessor`. The display text that DocsToData keeps in `UsageText` (pluralized unit
counts) moves into the library, driven by `IUsageVocabulary` display names.

### 2. The visual pass — a fast follow, not part of parity

Parity first, so DocsToData can switch without a visual regression to argue about. Then one pass at
making it look deliberately designed, with the `dataviz` guidance as the standard. Candidates, to be
settled when the pass starts:

- Capacity shown per grant rather than as one summed line, so a grant expiring mid-range is visible
  as a step.
- A remaining-balance bar per grant on `GrantList`, with an "expires in N days" callout.
- Empty states that say what to do next ("No grants — create one") instead of an empty table.
- Light and dark from one set of tokens.
- Display names, not tokens. DocsToData's grant list and delete prompt print the raw operation and
  unit (`document_extraction`, `page`); the editor's dropdowns already use `IUsageVocabulary` display
  names, and every other view should too.
- The overdraft made visible: a grant past its quantity reads as overdrawn, not as 100%.

### 3. A demo portal — `Corely.Billing.Demos.Portal`

A Blazor Server host that shows the components with nothing else in the way:

- No IAM. A fixed demo account id, returned by a two-line `IBillingAccountAccessor`, so the demo is
  about billing and nothing else.
- A "simulate usage" button that reserves and settles a random quantity under a fresh operation
  scope, so the chart moves while someone watches.
- A `--seed` switch that writes a spread of grants (expired, active, upcoming, overlapping) and
  several months of consumption, so the charts are worth looking at on first run.
- Schema from `corely-billing-db` on LocalDB, the same path a deployment takes, as IAM's demos do.

A demo that pairs Billing with IAM, and the demos for the other ways the library can be used, belong
to `usage-shapes-docs-and-demos.md`.

### 4. Tests

- **Unit — `Corely.Billing.Web.UnitTests` with bUnit**, as `Corely.IAM.Web.UnitTests` does:
  rendering per state (empty, unauthorized, a mix of grant statuses), the editor's validation, the
  filter and sort parameters reaching `ListConsumptionEventsRequest`, bucket choice by range.
- **Functional — one smoke test for the demo portal** with `WebApplicationFactory`: it starts, the
  routed pages answer, the static assets (the chart script included) are served. The circuit is out
  of this tier's reach, as IAM's demo tests found.

### 5. Docs

`Corely.Billing.Web/Docs/` per `DOCUMENTATION-STYLE.md`: `index.md`, a setup page, one page per
component, styling. Linked from the root README's documentation table.

## Afterwards: DocsToData moves onto the components

Once `Corely.Billing.Web` is published, DocsToData replaces its three pages with the components,
keeping only its own `IBillingAccountAccessor` and its `PermissionView` wrappers. Its Chart.js libman
entry and `usage-chart.js` go away. That is DocsToData work, tracked there, and waits for the parity
release rather than the visual pass.

## Decisions to make before building

1. **Package name.** `Corely.Billing.Web`, matching `Corely.IAM.Web`. Recommended.
2. **Render mode.** Blazor Server only, like IAM.Web, or also static SSR for the read-only views.
   Recommend Server only until a host asks.
3. **Routed pages at all.** Recommend yes, opt-in, since most hosts want exactly those four pages.

## Out of scope

- Any change to `Corely.Billing` itself, except whatever `usage-shapes-docs-and-demos.md` decides.
- Consumption entry from the UI. Consumption is written by the work being billed, through quota,
  never typed in.
- Invoicing, prices or money. Billing counts units; what a unit costs is the host's business.
