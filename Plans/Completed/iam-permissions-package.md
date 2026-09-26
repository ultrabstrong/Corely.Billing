# Corely.Billing.IAM and Corely.Billing.Web.IAM: Corely.IAM permissions, opt-in, server and UI

**Status: done.** Released as Corely.Billing, .Web and the CLI 2.0.0, with Corely.Billing.IAM and
Corely.Billing.Web.IAM 1.0.0. What happened is under "Outcome" at the end.

## The principle this plan serves

Corely.IAM is the gold standard for how permissions work in every Corely project. Every other
project (1) uses what Corely.IAM ships and (2) conforms to its shape. Inventing a new way of doing
something IAM already does is contrary to the point of having Corely.IAM and of the ecosystem in
general. The Corely ecosystem must feel like one contiguous system, where each component harmonizes
with and complements the others, and the components feel like different parts of a whole.

**This has been built before.** Corely.IAM and Corely.IAM.Web already implement this exact pattern
for accounts, users, groups, roles and permissions: a resource type constant, a decorator that
checks account context and then CRUDX with the item's id, lists scoped to the ids the caller may
read, and list and detail pages that wrap each Create, Update and Delete control in `PermissionView`.
Grants are one more resource type following the same pattern; the only difference is that they live
in a different project. When a detail below is unclear, the answer is whatever IAM does for roles.

If Corely.IAM does not provide something this plan needs, do not work around it here. Stop, and
assess the design of Corely.IAM with the owner. Nothing in this plan should need that: every
mechanism below is one IAM already ships and uses on its own pages.

## Starting cold

Read, in this order:

1. This repository's `CLAUDE.md`, `DOCUMENTATION-STYLE.md`, `Corely.Billing/Docs/authorization.md`
   and `Corely.Billing.Web/Docs/`.
2. Corely.IAM (`C:\source\git\ultrabstrong\Corely.IAM`), which is the reference for every decision:
   - `Corely.IAM/Docs/authorization.md` and `Corely.IAM/Docs/resource-types.md`
   - `Corely.IAM/Security/Providers/IAuthorizationProvider.cs`
   - `Corely.IAM/Roles/Processors/RoleProcessorAuthorizationDecorator.cs`, the pattern every
     decorator below copies
   - `Corely.IAM.Web/Components/Shared/PermissionView.razor` and `Corely.IAM.Web/Docs/authorization-ui.md`
   - `Corely.IAM.Web/Components/Pages/Roles/RoleList.razor` and `RoleDetail.razor`, the pattern the
     grant screens copy
   - `Corely.IAM.Web.UnitTests/Components/PermissionViewTests.cs`, the bUnit setup to reuse
3. The code this work replaces, in DocsToData (`C:\source\git\pinnacleinnovation\DocsToData`),
   read-only here:

| File | Holds |
|---|---|
| `DocsToData.Authorization/GrantAuthorizationDecorator.cs` | Grants: Create, Read, Update, Delete on `grants`, type-level only |
| `DocsToData.Authorization/ConsumptionAuthorizationDecorator.cs` | Consumption reads: Read on `metering` |
| `DocsToData.Authorization/QuotaAuthorizationDecorator.cs` | Availability: Read on `quota`. Reserve, settle, release: Read on `quota` **and** Create on `metering` |
| `DocsToData.Authorization/ServiceRegistrationExtensions.cs` | `AddBillingAuthorization()`, passed to `BillingOptions.DecorateServices` |
| `DocsToData.Authorization.Tests/` | Their tests, including the quota case of Read without metering Create |
| `DocsToData.Core/Constants/PermissionConstants.cs` | The resource type strings |
| `DocsToData.AdminPortalWebApp/Services/BillingAccountAccessor.cs` | The hand-written `IBillingAccountAccessor`, Update on `grants` for `CanManageGrantsAsync` |

4. The demo that already hand-writes this: `Corely.Billing.Demos.WithIAM` (`Authorization/`,
   `IamBillingAccountAccessor.cs`, `Program.cs`).

## Why

Corely.Billing has no notion of permissions, by design: a host adds authorization through
`BillingOptions.DecorateServices`, which keeps Billing free of Corely.IAM. The cost is that every IAM
host writes the same decorators, picks its own resource type names, and has to remember to register
those names with IAM. That last step has already been missed: DocsToData never calls
`IAMOptions.RegisterResourceType`, so IAM's `PermissionValidator` rejects a `grants` or `quota`
permission and the IAM.Web permission dropdown does not list them.

**The UI lost per-action permissions, and this plan restores them.** Corely.Billing.Web 1.0.0 shipped
with one flag, `IBillingAccountAccessor.CanManageGrantsAsync` and a `CanManage` parameter, that shows
or hides New, Edit and Delete together. `web-components-and-demos.md` had asked for "a
`RenderFragment` slot or a `CanManage` parameter. An IAM host wraps them in `PermissionView`"; the
build took the flag and never raised the choice. DocsToData's own pages had wrapped each button in
IAM's `PermissionView` with its own action. When DocsToData moved onto the components (its
`Plans/Completed/corely-billing-web-and-unlimited-grants.md`), a role with Update but not Create or
Delete started seeing New and Delete, which the server then refuses. The server never let anything
through; the screen just stopped matching the permissions. The fix is to put `PermissionView` back
around each action, which needs a seam in Corely.Billing.Web and the IAM half in a package.

## The shape: two packages, mirroring Corely.IAM and Corely.IAM.Web

| Package | References | Holds |
|---|---|---|
| `Corely.Billing.IAM` | `Corely.Billing`, `Corely.IAM` | Resource type constants, the three service decorators, `UseCorelyIamPermissions()`, `RegisterBillingResourceTypes()` |
| `Corely.Billing.Web.IAM` | `Corely.Billing.Web`, `Corely.IAM.Web`, `Corely.Billing.IAM` | The `PermissionView` gate for grant actions, the IAM account accessor, `AddBillingWebIam()` |

Two packages, not one, for the same reason Corely.IAM splits `Corely.IAM` from `Corely.IAM.Web`: a
host without a UI must be able to take the checks without taking Blazor. DocsToData's Functions app
is exactly that host: it decorates the Billing services and has no web UI.

A host that installs neither gets what it has today: no checks, every control shown.

## Part 1: `Corely.Billing.IAM` (server)

### Resource types

`BillingResourceTypes`, public static, `SCREAMING_SNAKE_CASE` constants shaped like IAM's
`PermissionConstants`: one constant per resource type, plus a description for each, used by the
registration below. The names are `grant`, `consumption` and `quota` (decision 1): singular, like
IAM's own, and each named for the Billing service it guards. Every check in both packages references these
constants; no string literal for a resource type appears anywhere else.

```csharp
services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
```

`RegisterBillingResourceTypes()` is an extension on `IAMOptions` that calls IAM's own
`RegisterResourceType(name, description)` once per constant and returns the options. It uses IAM's
registration exactly as a host would; it adds no registry of its own, and Corely.IAM does not
change (decision 3).

### Decorators

Three decorators, `GrantAuthorizationDecorator`, `ConsumptionAuthorizationDecorator` and
`QuotaAuthorizationDecorator`, `internal sealed`, each taking the inner service and
`IAuthorizationProvider` by primary constructor. **Each method is written the way
`RoleProcessorAuthorizationDecorator` writes it:** an account check, then a CRUDX check, then either the
inner call or the service's `UnauthorizedError` result. That means three things the DocsToData
decorators do not do today, and which this package must do:

1. **Account context first.** Every method whose request carries an account id checks
   `HasAccountContext(accountId)` before `IsAuthorizedAsync`, exactly as IAM does. Without it a user
   authorized for grants in their own account could read or change another account's grants by
   passing its id. System context passes `HasAccountContext`, so pipelines running as the system are
   unaffected.
2. **Resource ids on single-grant actions.** Get, update and delete of one grant pass that grant's id
   to `IsAuthorizedAsync`, as IAM passes `roleId`. Create and list are type-level.
3. **Lists are scoped to what the caller may read.** As `ListRolesAsync` does: after the type-level
   Read check passes, the decorator calls `GetAuthorizedResourceIdsAsync(AuthAction.Read, grants)`
   and hands the result to the inner service. Never the caller's set; it could only widen it. See
   "The one Corely.Billing change" for where that set goes.

Denials return the result code the service already defines (`UnauthorizedError`) with a message in
IAM's wording: `Unauthorized to create grant`, `Unauthorized to read grant {grantId}`,
`Unauthorized to list grants`, `Unauthorized to update grant {grantId}`,
`Unauthorized to delete grant {grantId}`, and for consumption and quota the same form naming the
operation.

**The permission matrix.** "Grants", "consumption" and "quota" below stand for the three resource
type constants, whatever decision 1 names them.

| Service method | Account check | Permission |
|---|---|---|
| `CreateGrantAsync` | `request.AccountId` | Create on grants |
| `GetGrantAsync` | `accountId` | Read on grants, `grantId` |
| `ListGrantsAsync` | `request.AccountId` | Read on grants, then scoped to `GetAuthorizedResourceIdsAsync(Read, grants)` |
| `UpdateGrantAsync` | `request.AccountId` | Update on grants, `request.GrantId` |
| `DeleteGrantAsync` | `accountId` | Delete on grants, `grantId` |
| Every `IConsumptionService` read | the method's account id | Read on consumption, type-level |
| `GetAvailabilityAsync` | `accountId` | Read on quota |
| `ReserveAsync`, `SettleAsync`, `ReleaseAsync` | the request's account id | Execute on quota |

Consumption and quota checks are type-level. A consumption event and a quota hold are records of work,
not resources anyone assigns permissions to one by one, so there is no id to pass. Every consumption
and quota method carries an account id, so every one gets the account check.

**Every quota method checks quota permissions and nothing else (decision 4).** The quota service
reads grants and writes consumption internally, but whoever assigns permissions should not need to
know that: Read on quota to check availability, Execute on quota to reserve, settle or release. One
permission for all three writes also means a caller can never hold quota it cannot settle or
release. Grant and consumption permissions guard only their own services. This replaces DocsToData's
"Read on quota and Create on metering".

`GetAvailabilityAsync` when denied returns `QuotaAvailability.Unauthorized` (decision 5), a new
member added by this plan. It is not `Unknown`, which reads as "could not tell, let it through", and
not `Exhausted`, which would tell the caller the account is out of quota when it is not.

### Registration

```csharp
services.AddBillingServices(
    BillingOptions.Create(configuration, efConfig)
        .RegisterOperation(...)
        .UseCorelyIamPermissions());
```

`UseCorelyIamPermissions()` is an extension on `BillingOptions` that passes a registration of the
three decorators to the existing `DecorateServices` hook. They sit inside the telemetry decorators,
as DocsToData's do today, so a denied call is still recorded. Not calling it registers nothing.

When `IAuthorizationProvider` is not registered, `UseCorelyIamPermissions()` throws while it
registers, with a message naming `AddIAMServices` (decision 2). It inspects the
`IServiceCollection`, so `AddIAMServices` must be called before `AddBillingServices`, as IAM's own
setup already asks for an order.

### The Corely.Billing changes

Two, both breaking, so Corely.Billing goes to 2.0.0 (decision 6):

- `QuotaAvailability` gains `Unauthorized`, for the denied availability check above.
- `IGrantService.ListGrantsAsync` gains a parameter, below.

To scope a list the way IAM does, `IGrantService.ListGrantsAsync` takes the same trailing parameter
IAM's list processors take, with the same name and meaning:

```csharp
Task<RetrieveListResult<Grant>> ListGrantsAsync(
    ListGrantsRequest request,
    IReadOnlySet<Guid>? authorizedResourceIds = null,
    CancellationToken ct = default);
```

`null` means unrestricted, as in IAM. The service narrows its query with
`GuidFilter.In(...)` on `GrantId` when the set is not null, combined with the request's own filter,
and never modifies the caller's `FilterBuilder`. Corely.Billing stays free of IAM: it only receives a
set of ids. This adds a parameter to a public interface, so anything implementing `IGrantService`
must follow; that is every decorator, including DocsToData's until they are deleted.

## Part 2: `Corely.Billing.Web` (the seam)

Corely.Billing.Web does not reference Corely.IAM, and still will not. It gets a seam that lets
`PermissionView` wrap each action without Billing.Web knowing IAM exists. The seam is the shape
`PermissionView` already has, an action, a resource id, and authorized and not-authorized content, so
the IAM half is a direct pass-through.

```csharp
namespace Corely.Billing.Web;

public enum GrantAction
{
    Create,
    Update,
    Delete,
}

public interface IGrantActionGate
{
    RenderFragment Gate(
        GrantAction action,
        Guid? grantId,
        RenderFragment authorized,
        RenderFragment? notAuthorized = null);
}
```

- `OpenGrantActionGate`, internal, returns `authorized` unchanged. `AddBillingWeb<T>()` registers it
  with `TryAddScoped`, so a host with no IAM sees every control, as it does today.
- `GrantAction` has only the actions the screens offer. Reading is not gated in the UI: a list or
  editor the caller may not read already shows the service's `UnauthorizedError` message, as IAM's
  pages do.

**Remove the flag.** Delete `IBillingAccountAccessor.CanManageGrantsAsync`, the `CanManage`
parameter on `GrantList` and `GrantEditor`, and `BillingPageBase.CanManage`. The accessor keeps only
`GetAccountIdAsync`. This is a breaking change to a public API, so Corely.Billing.Web goes to 2.0.0.

**Every write control goes through the gate**, injected as `IGrantActionGate` and called with inline
Razor templates (`@<a ...>...</a>`), matching IAM's `RoleList` and `RoleDetail`:

| Component | Control | Gate call |
|---|---|---|
| `GrantList` | "New grant" in the toolbar | `Create`, no id |
| `GrantList` | the empty state | `Create`, no id. Authorized: the "create the first grant" text and link. Not authorized: "Grants given to this account will appear here." |
| `GrantList` | a row's edit link | `Update`, the grant's id. Authorized: the Edit link. Not authorized: a View link to the same editor route, as IAM's lists always offer View |
| `GrantList` | a row's delete button and its inline confirm | `Delete`, the grant's id |
| `GrantEditor`, new grant | the form | `Create`, no id. Not authorized: "You are not allowed to create grants." |
| `GrantEditor`, existing grant | the form | `Update`, the grant's id. Authorized: the editable form and Save. Not authorized: the same fields read-only with "You can view this grant but not change it." |

For the editor, render the form from one private `RenderFragment` taking `bool editable`, and pass
`Form(true)` and `Form(false)` as the two fragments, so the read-only view cannot drift from the
editable one.

`UsageChart`, `ConsumptionTable` and `UsageDashboard` have no write controls and do not change.

## Part 3: `Corely.Billing.Web.IAM` (the UI half)

Three things, and each uses what IAM ships as-is:

1. **`PermissionViewGrantActionGate : IGrantActionGate`**, internal sealed. `Gate` returns a fragment
   that renders IAM's `PermissionView`, unmodified, with `Action` mapped from the `GrantAction`,
   `Resource` set to the grants constant from `BillingResourceTypes`, `ResourceIds` set to
   `[grantId]` when there is one and left null when there is not, and the two fragments passed to
   `Authorized` and `NotAuthorized`. Build it with `RenderTreeBuilder.OpenComponent<PermissionView>`.
   The `GrantAction` to `AuthAction` mapping is a `GrantActionExtensions` extension block in this
   package's `Extensions/` folder (`ToAuthAction()`), with its own tests, per `CLAUDE.md`'s rule for
   conversions of a type this layer cannot change. It maps by name and has no default arm.
2. **`IamBillingAccountAccessor : IBillingAccountAccessor`**, internal sealed. It returns
   `(await IBlazorUserContextAccessor.GetUserContextAsync())?.CurrentAccount?.Id`, the same code the
   WithIAM demo and DocsToData each hand-wrote.
3. **`AddBillingWebIam()`**, an extension on `IServiceCollection`. It calls
   `AddBillingWeb<IamBillingAccountAccessor>()`, then
   `services.Replace(ServiceDescriptor.Scoped<IGrantActionGate, PermissionViewGrantActionGate>())`.

`PermissionView` injects `IAuthorizationProvider` and `IUserContextProvider`, both scoped. The Billing
components already share the circuit's scope (see `110cc88`), so the gate sees the signed-in user.
Keep it that way: a component resolving services in a scope of its own would see no user, and
every gate would hide everything.

A host with IAM then writes:

```csharp
builder.Services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
builder.Services.AddBillingServices(billingOptions.UseCorelyIamPermissions());
builder.Services.AddBillingWebIam();
```

and nothing else: no accessor, no decorators, no `PermissionView` wrappers of its own.

## The demo: `Corely.Billing.Demos.WithIAM`

This is the showcase; enhance it and do not add another demo.

- Delete its `Authorization/` folder and `IamBillingAccountAccessor.cs`. Call
  `RegisterBillingResourceTypes()`, `UseCorelyIamPermissions()` and `AddBillingWebIam()` instead.
- Seed a third user alongside `olivia` (owner) and `bobby` (no roles): a role with Read and Update on
  grants and Read on consumption, but not Create or Delete. Signed in as that user, the grants page
  shows no New grant and no Delete, and each row's link says Edit. That user is the proof that the
  per-action gates work, and the README says so.

## Tests

- **`Corely.Billing.IAM.UnitTests`:** the decorator tests move from DocsToData and grow to cover what
  DocsToData never checked: a denied account context, the grant id reaching `IsAuthorizedAsync` for
  get, update and delete, and the list passing `GetAuthorizedResourceIdsAsync`'s set rather than
  anything from the request. `RegisterBillingResourceTypes` registers every constant.
- **`Corely.Billing.Web.UnitTests`:** each gated control asks the gate for the action and id in the
  table above. Use a recording `IGrantActionGate` fake, and assert on the calls and on which fragment
  rendered.
- **`Corely.Billing.Web.IAM.UnitTests`,** bUnit, with the setup `PermissionViewTests` uses (mocked
  `IAuthorizationProvider` and `IUserContextProvider`): `GrantList` with Update but not Create or
  Delete renders Edit and neither New nor Delete; with nothing but Read renders View links only; the
  editor renders read-only without Update. `ToAuthAction` maps every member.
- **Integration, `Corely.Billing.IntegrationTests`:** `ListGrantsAsync` with an authorized set
  returns only those grants, and with `null` returns all.
- **Functional:** the WithIAM demo smoke test still passes.

**Prove the gate test catches the regression:** make `PermissionViewGrantActionGate` pass
`AuthAction.Update` for every action, watch the Create and Delete assertions go red, then restore.

## Part 4: DocsToData takes the packages

Part of this plan, done by the same session once the packages are released: DocsToData is their
first consumer, and wiring it up is how this work proves itself. That work is in
`C:\source\git\pinnacleinnovation\DocsToData` and follows **that repository's** `CLAUDE.md`, which
differs from this one in ways that matter: push only when the owner says so, one commit at a time
(every push costs metered CI minutes and storage); click a portal change through on the local stack
at desktop and phone widths and run the opt-in browser tests before pushing (the `local-stack` skill
drives both); and nothing towards prod.

- Bump `Corely.Billing` and `Corely.Billing.Web` in `Directory.Packages.props`, and add
  `Corely.Billing.IAM` and `Corely.Billing.Web.IAM`. `Corely.Billing.Web.IAM` goes in the portal's
  csproj; `Corely.Billing.IAM` goes wherever `DocsToData.Authorization` is referenced today.
- Delete `DocsToData.Authorization/` and `DocsToData.Authorization.Tests/`, and remove both from the
  solution. Replace `.DecorateServices(s => s.AddBillingAuthorization())` with
  `.UseCorelyIamPermissions()` in every host that calls it: the portal (`Program.cs`), Functions and
  the console app (their `ServiceFactory.cs`).
- Call `RegisterBillingResourceTypes()` on `IAMOptions` in every host that builds one.
- Delete `DocsToData.AdminPortalWebApp/Services/BillingAccountAccessor.cs` and
  `DocsToData.AdminPortalWebApp.UnitTests/Services/BillingAccountAccessorTests.cs`; replace
  `AddBillingWeb<BillingAccountAccessor>()` with `AddBillingWebIam()`.
- The portal's `Grants.razor`, `GrantEditor.razor` and `Usage.razor` inherit `BillingPageBase`; remove
  every use of `CanManage` from them.
- Replace the billing values in `DocsToData.Core/Constants/PermissionConstants.cs` with
  `BillingResourceTypes` wherever DocsToData references them, and delete the replaced constants.
- Decision 1 renames `grants` to `grant` and `metering` to `consumption`. Rename any existing
  permission rows in the same change; today DocsToData has only wildcards, so there are none.
- Decision 4 moves reserve, settle and release to Execute on quota. The pipeline runs as the system
  and passes every check, so nothing it does changes; a user role that consumed quota needs Execute
  on quota instead of Read on quota plus Create on metering.
- Seed the local stack (`iac/local`) with a user holding Read and Update on grants but not Create or
  Delete, as the WithIAM demo does, and click through as that user: no New grant, no Delete, Edit on
  each row.
- Move DocsToData's `Plans/Completed/corely-billing-web-and-unlimited-grants.md` Outcome forward by one
  line pointing at this plan, so the next reader knows the hand-written accessor is gone.

Out of scope, a DocsToData follow-up: registering DocsToData's own types (`extraction`, `sftp`,
`document_workflows`) the same way, and deleting the unused `PermissionConstants.ENTITLEMENTS`.

## Owner decisions

Settled with the owner; the body above is written to match.

1. **Resource type names: `grant`, `consumption`, `quota`.** Singular like IAM's own, each named for
   the service it guards. DocsToData's `grants` and `metering` are renamed; only wildcards exist, so
   no rows move.
2. **The IAM-registered check runs at registration.** `UseCorelyIamPermissions()` inspects the
   `IServiceCollection` and throws, naming `AddIAMServices`, when `IAuthorizationProvider` is missing.
   `AddIAMServices` comes first.
3. **The host calls `iamOptions.RegisterBillingResourceTypes()`.** An extension on `IAMOptions` in
   Corely.Billing.IAM, calling IAM's existing `RegisterResourceType`. Corely.IAM does not change.
4. **Quota is authorized by quota permissions only.** Read on quota for `GetAvailabilityAsync`;
   Execute on quota for reserve, settle and release. Someone assigning permissions should not need
   to know that quota reads grants and writes consumption.
5. **A denied availability check returns a new `QuotaAvailability.Unauthorized`.**
6. **Versions.** Corely.Billing 2.0.0 (a changed interface signature and a new enum member);
   the migration tool 2.0.0, because its major follows Corely.Billing's, with no schema change;
   Corely.Billing.Web 2.0.0; Corely.Billing.IAM and Corely.Billing.Web.IAM 1.0.0. The new packages
   take Corely.IAM 2.3.1 and Corely.IAM.Web 2.4.0 as minimum versions, NuGet's default rule.

## Relation to other plans

- DocsToData's `Plans/Completed/corely-billing-web-and-unlimited-grants.md` moved its pages onto
  the 1.0.0 components with a hand-written accessor and its own decorators. This plan replaces both.
- `web-components-and-demos.md` and `usage-shapes-docs-and-demos.md` are done. The first is where
  the single flag came from.

## Done when

The owner decisions are answered in this file; both packages and the Corely.Billing and
Corely.Billing.Web changes are built, tested (including the break-and-restore check) and released;
the WithIAM demo shows the read-and-update user with no New or Delete; DocsToData uses the packages
instead of its own decorators and accessor, its full `RebuildAndTest.ps1` and opt-in browser tests
pass, and its portal has been clicked through locally as that same kind of user; and a permission for
each billing resource type can be created in the IAM admin UI. DocsToData is committed locally and
pushed only when the owner says so.

## Outcome

Built as written; nothing needed a change to Corely.IAM.

- **Released:** tag `v2.0.0`. Corely.Billing, Corely.Billing.Web and `corely-billing-db` at 2.0.0;
  Corely.Billing.IAM and Corely.Billing.Web.IAM at 1.0.0, both packed by `release.yml` and checked
  by `check-package-versions.sh`.
- **Server:** the three decorators copy `RoleProcessorAuthorizationDecorator`; `ListGrantsAsync`
  takes `authorizedResourceIds` and narrows its query the way IAM's `ListQueryHelper` does.
  `QuotaAvailability.Unauthorized` is new for a denied availability read.
- **UI:** `IGrantActionGate` in Corely.Billing.Web, open by default; Corely.Billing.Web.IAM swaps in
  a `PermissionView` gate and `IamBillingAccountAccessor`. `CanManage` is gone.
- **Tests:** 39 in Corely.Billing.IAM.UnitTests, including registration through the real
  `AddIAMServices`; 10 bUnit tests in Corely.Billing.Web.IAM.UnitTests. Forcing every action to Update in
  `ToAuthAction` turns two of them red.
- **Demo:** WithIAM drops its own decorators and accessor and seeds `carla` with a Grant editor role.
  Clicked through as olivia (everything), carla (Edit only) and bobby (nothing).
- **DocsToData:** takes the packages in one commit, `d9f1aff`, pushed on the owner's say-so and
  deployed to dev. `DocsToData.Authorization`
  and the portal's accessor are deleted, and the local seed gains `editor` with the same role as
  carla. The IAM admin UI's Create Permission offers `grant`, `consumption` and `quota`. Clicked
  through as editor and admin, desktop and phone; the full suite and the browser tests pass.
