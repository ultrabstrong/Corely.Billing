# Corely.Billing.IAM: permission checks from Corely.IAM, opt-in

**Status: proposal for discussion.** The owner agrees with the shape in principle and wants to review
the details in a session before anything is built. That session's job is to settle the open questions
at the end with the owner, update this plan, and only then build.

## Starting cold

Read this repository's `CLAUDE.md`, `DOCUMENTATION-STYLE.md` and `Corely.Billing/Docs/authorization.md`,
and Corely.IAM's `Corely.IAM/Docs/resource-types.md` and its authorization docs
(`C:\source\git\ultrabstrong\Corely.IAM`). The code this package would absorb is in DocsToData
(`C:\source\git\pinnacleinnovation\DocsToData`), read-only for this work:

| File | Holds |
|---|---|
| `DocsToData.Authorization/GrantAuthorizationDecorator.cs` | Grants: Create, Read, Update, Delete on `grants` |
| `DocsToData.Authorization/ConsumptionAuthorizationDecorator.cs` | Consumption reads: Read on `metering` |
| `DocsToData.Authorization/QuotaAuthorizationDecorator.cs` | Availability: Read on `quota`. Reserve, settle, release: Read on `quota` **and** Create on `metering` |
| `DocsToData.Authorization/ServiceRegistrationExtensions.cs` | `AddBillingAuthorization()`, passed to `BillingOptions.DecorateServices` |
| `DocsToData.Authorization.Tests/` | Their tests, including the quota case of Read without metering Create |
| `DocsToData.Core/Constants/PermissionConstants.cs` | The resource type strings |

## Why

Corely.Billing has no notion of permissions, by design: a host adds authorization through
`BillingOptions.DecorateServices`. That keeps Billing free of Corely.IAM, but every IAM host writes
the same three decorators DocsToData wrote, picks its own resource type names, and has to remember to
register those names with IAM.

That last step has already been missed. DocsToData never calls `IAMOptions.RegisterResourceType`, so
IAM's `PermissionValidator` rejects a `grants` or `quota` permission and the Corely.IAM.Web
permission dropdown does not list them. Today only the wildcard `*` permission reaches any of
DocsToData's resources, billing or otherwise.

## The shape

A separate package, **`Corely.Billing.IAM`**, referencing Corely.Billing and Corely.IAM. Corely.Billing
itself stays free of IAM; only a host that installs this package takes the dependency.

```csharp
services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
services.AddBillingServices(
    BillingOptions.Create(configuration, efConfig)
        .RegisterOperation(...)
        .UseCorelyIamPermissions());
```

- **`UseCorelyIamPermissions()`**, an extension on `BillingOptions`, adds three decorators around
  `IGrantService`, `IConsumptionService` and `IQuotaService` through the existing `DecorateServices`
  hook. They sit inside the telemetry decorators, as the host's do today, so a denied call is recorded.
- **Not calling it means no checks.** Nothing is registered, so no pass-through decorator is needed.
- **Resource type names ship as constants**, e.g. `BillingResourceTypes.GRANTS`, so every host uses
  the same strings and the web components can reference them.
- **The IAM-registered check.** The package fails at startup with a clear message when
  `IAuthorizationProvider` is not registered. See open question 2 for when that check can run.

DocsToData afterwards: delete its three decorators, their tests and `AddBillingAuthorization`, replace
`.DecorateServices(s => s.AddBillingAuthorization())` with `.UseCorelyIamPermissions()`, and register
the resource types. A follow-up in DocsToData registers its own non-billing types (`extraction`,
`sftp`, `document_workflows`) the same way, and deletes the unused `PermissionConstants.ENTITLEMENTS`.

## The demo already exists: `Corely.Billing.Demos.WithIAM`

The showcase for this package is `Corely.Billing.Demos.WithIAM`, which is already built. Enhance it;
do not add another demo. Today it hand-writes what this package would provide, and those pieces are
the starting point for the package:

- `Authorization/GrantAuthorizationDecorator.cs` and `ConsumptionAuthorizationDecorator.cs`, passed
  to `BillingOptions.DecorateServices`
- `Program.cs` registering the `grants` and `usage` resource types with `IAMOptions`
- `IamBillingAccountAccessor.cs`, with `CanManageGrantsAsync` asking IAM for Update on `grants`

When the package ships, the demo deletes its `Authorization/` folder, calls
`.UseCorelyIamPermissions()` and the package's resource-type registration instead, and takes the
resource names question 1 settles. Its seeded member `bobby`, who has no roles, is the case that
shows a denial. Add whatever else the package needs to show, a user with read but not write
permission for one, to that seed.

It also records a constraint the package must keep: Corely.Billing.Web's components share the
circuit's DI scope, because IAM's user context is scoped. Decorators resolved in a fresh scope would
see no user and deny everything.

## Open questions for the discussion

1. **Resource type names.** DocsToData uses `grants`, `metering` and `quota`; the WithIAM demo
   already registers `grants` and `usage`. Pick one set for the library's vocabulary. Existing
   permission rows would need renaming; today DocsToData has none except wildcards, so this is the
   cheapest moment.
2. **Where the IAM check runs.** `AddBillingServices` can inspect the `IServiceCollection` for
   `IAuthorizationProvider`, but that makes the registration order matter (IAM first). The
   alternatives are a check at first resolution, or an `IValidateOptions`/startup filter. Which does
   the owner prefer: strict ordering with an immediate error, or order-free with a later one?
3. **Registering resource types with IAM.** IAM builds its registry from `IAMOptions` inside
   `AddIAMServices`, so Billing cannot add to it afterwards. Options: (a) a second call the host makes,
   `iamOptions.RegisterBillingResourceTypes()`, as sketched above; (b) a change in Corely.IAM that
   lets other packages contribute types through DI; (c) `UseCorelyIamPermissions` checks the registry
   at startup and fails if the types are missing. (a) is simplest; (b) makes one call enough.
4. **The permission matrix.** Is "quota Read plus metering Create" the right rule for reserve, settle
   and release, or should quota get its own write action? And should consumption reads stay under
   Read on the consumption resource?
5. **Availability when denied.** DocsToData's decorator returns `QuotaAvailability.Unknown`, which
   callers treat as "let it through". Correct for a pipeline that fails open; is it right for a
   library default, or should a denial be `Exhausted`?
6. **Versioning.** Does the package version with Corely.Billing (same number, released together) or
   on its own, and what range of Corely.IAM does it accept?
7. **Tests.** Unit tests for the decorators move from DocsToData. Does the package also need an
   integration test with real IAM (a user with and without each permission), or is that DocsToData's?

## Relation to other plans

- `Corely.Billing.Web` shipped with `IBillingAccountAccessor.CanManageGrantsAsync` (defaults to
  true), which hosts answer themselves. With this package, it could answer from IAM's grants
  permission. Decide whether the package provides that accessor or leaves it to the host.
- DocsToData's `Plans/New/corely-billing-web-and-unlimited-grants.md` moves its pages onto the
  components with a hand-written accessor and its existing decorators. Whichever lands second
  replaces the hand-written pieces.
- Done and no longer blocking: `usage-shapes-docs-and-demos.md`, `web-components-and-demos.md`
  (both in `Completed/`) and Corely.IAM's keyed EF configuration.

## Done when

The open questions are answered in this file; the package is built, tested and released; DocsToData
uses it instead of its own decorators and registers its resource types; and a permission for each
billing resource type can be created in the IAM admin UI.
