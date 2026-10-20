# Corely.Billing.Web: components only, the host owns every path

**Status: released** as `Corely.Billing.Web` 3.0.0 and `Corely.Billing.Web.IAM` 3.0.0 (on Corely.IAM.Web
7.0.0), with `Corely.Billing.Web` 3.0.1 fixing a component that loaded before its scope signed in. Second of three
plans in order: after
`Corely.IAM/Plans/web-components-only.md`, before `DocsToData/Plans/New/account-scoped-routes.md`.

## Why

Corely.Billing.Web already does most of this right: `GrantList`, `GrantEditor`, `GrantRequestForm`,
`UsageDashboard` and the rest do the work, and the host passes the links (`GrantList`'s
`NewGrantHref`, `RequestGrantHref`, `GrantHref`). Two things still decide the host's paths:

1. **Five opt in routed pages** in `Corely.Billing.Web/Pages` (`GrantsPage` at `/grants`,
   `GrantEditorPage` at `/grants/new` and `/grants/{id}`, `GrantRequestPage` at `/grants/request`,
   `UsagePage` at `/usage`), routed when a host adds the assembly to its router.
2. **Default links** from `BillingWebRoutes`, which every link parameter falls back to
   (`GrantList.razor.cs`: `NewGrantHref = BillingWebRoutes.GRANT_NEW` and so on).

The owner decided (2026-10-20) that the Corely libraries ship components only and the host owns
every path, matching Corely.IAM.Web's change. So both go.

## Design

1. **The routed pages leave the library.** `GrantsPage`, `GrantEditorPage`, `GrantRequestPage` and
   `UsagePage` (with their `.razor.css`) move into the demo hosts that use them as shells:
   `Corely.Billing.Demos.Portal` and `Corely.Billing.Demos.WithIAM` (both route the Billing.Web
   assembly today: `grep -r AdditionalAssemblies`). Each demo then drops the assembly from its
   `Routes.razor` and its `AddAdditionalAssemblies`.
2. **`BillingPageBase` stays in the library** (owner's decision, 2026-10-20). Hosts' shells inherit it
   (DocsToData's do): it reads the account through `IBillingAccountAccessor`, handles loading and owns
   the scope, so a shell does not repeat that code. Only the pages go, not the base.
   Its namespace (`Corely.Billing.Web.Pages`) may move to something like `Corely.Billing.Web` now that
   no page lives beside it; if it does, the migration guide says so.
3. **`BillingWebRoutes` goes.** Every link parameter that defaulted to it becomes
   `[Parameter, EditorRequired]` with no default: `GrantList.NewGrantHref`, `RequestGrantHref`,
   `GrantHref`, and any other (`grep -r BillingWebRoutes Corely.Billing.Web`; the `GrantRequestPage`
   and `GrantEditorPage` uses go with the pages). The demos pass their own paths.
4. **Components that navigate after an action** (an editor's save or cancel) already take callbacks
   (`OnSaved`, `OnCancel` in DocsToData's use); check none falls back to a library path.
5. **Account paths are the host's choice.** The components take `AccountId` as a parameter and the
   links as strings, so a host gives them `/accounts/<id>/grants/...` without anything here knowing.
   `Corely.Billing.Demos.WithIAM` shows account paths with Corely.IAM.Web's `AccountScope`, as the
   worked example (owner's decision, 2026-10-20); the Portal demo, which has no IAM, keeps simple
   paths.

## Corely.Billing.Web.IAM

It references `Corely.IAM.Web` (6.1.0 today). Move it to Corely.IAM.Web 7.0.0 once that is released,
and fix anything it used that IAM removed (it should use only services and `PermissionView`, not
pages; check with a build).

## Documentation

1. `Corely.Billing.Web/Docs/pages.md` becomes "Hosting the components": a shell per component,
   copied from the demos, and the rule that the host owns the paths. Remove `BillingWebRoutes` from
   every doc (`components/grant-list.md`, `components/grant-editor.md`).
2. `Corely.Billing.Web/Docs/setup.md` and `index.md`: no routed pages, no assembly in the router.
3. A migration guide at the repository root for moving to Corely.Billing.Web 3: write the shells
   (or keep using the demos' as a starting point), pass every link explicitly.
4. This repository's `CLAUDE.md`: the solution table's `Corely.Billing.Web` row says "Blazor Server
   components" (drop "and opt-in routed pages").

## Tests

1. **bUnit (`Corely.Billing.Web.UnitTests`):** link parameters are rendered as given; a test that no
   routed Blazor page remains in the assembly (`@page` scan, or `RouteAttribute` on any type).
2. **Functional (`Corely.Billing.Web.FunctionalTests/Demos`):** the demo hosts still start and serve
   the moved pages at their paths.
3. `.\RebuildAndTest.ps1` green before anything is tagged.

## Release

1. `Corely.Billing.Web` **3.0.0** (pages and `BillingWebRoutes` removed, link parameters now
   required: breaking).
2. `Corely.Billing.Web.IAM` **3.0.0** (its Corely.IAM.Web dependency moves a major version).
3. `Corely.Billing`, `Corely.Billing.IAM` and the migration CLI do not change.
4. As with Corely.IAM: verify against DocsToData through a local package feed first, then tag
   `Corely.Billing.Web-v3.0.0` and `Corely.Billing.Web.IAM-v3.0.0` **only with the owner's say so**,
   after this plan moves to `Plans/Completed/`.

## As built

1. **`BillingPageBase` moved to `Corely.Billing.Web.Components`**, beside `BillingComponentBase`, and
   its `AccountId` became `CurrentAccountId`. A shell at an account path takes an `AccountId` route
   parameter, which would otherwise collide with the base's property; the new name matches
   Corely.IAM.Web's `AuthenticatedPageBase.CurrentAccountId` and says which account it is.
2. **The WithIAM shells load for the signed in account.** Each inherits `BillingPageBase`, wraps its
   content in `AccountScope`, passes `CurrentAccountId` to the component and uses the route's
   `AccountId` only for links, so the URL is never an authorization input. They carry `[Authorize]`;
   the Portal shells keep the old pages' shape with `PortalRoutes` paths.
3. **The demos' paths live in each demo** (`PortalRoutes`, `DemoRoutes`), not in the library.
   `Corely.Billing.Web.IAM` needed no code change for Corely.IAM.Web 7, only the package version.
4. **The editor and request form already navigated only through callbacks**; nothing fell back to a
   library path once the pages left.
5. **Docs:** `Docs/pages.md` became `Docs/hosting-components.md`; the migration guide is
   `MIGRATION-Billing.Web-3.0.md`.
6. **Tests:** `HostOwnedPathsTests` fails on any `RouteAttribute` in the assembly (adding `@page` to
   a component turned it red; restored). `GrantListTests` and `PermissionGateTests` pass host links
   and assert they render as given. `WithIamDemoTests` proves each account shell answers and sends an
   anonymous visitor to sign in with the page as return URL; `PortalDemoTests` serves the moved
   pages at the demo's paths.
7. **DocsToData check:** the portal built and passed its unit and functional tests on the local
   packages, and its grants list, grant editor and usage dashboard rendered with the portal's links
   and no console errors.
8. **3.0.1: a component waits for its scope to sign in.** DocsToData's grants page, inside
   `AccountScope`, showed "You are not allowed to view grants". `BillingComponentBase` signed its
   scope in from `OnInitializedAsync`, but Blazor calls `OnParametersSetAsync` straight away for
   parameters that arrive while that is still awaiting, and `AccountScope`'s re-render sends them;
   `GrantList` then listed grants on an unsigned scope. The base now seals `OnParametersSetAsync`,
   awaits the sign in, and calls `OnParametersSetSignedInAsync`, which the five loading components
   override instead. `BillingComponentBaseTests` re-sends parameters during a held sign in and fails
   without the wait.
