# IAM 5

**Status: done.** Released as Corely.Billing 2.1.2, Corely.Billing.IAM 2.4.0, Corely.Billing.Web.IAM
2.3.0 and the migration CLI 2.0.4. Corely.Billing.Web did not change and was not released.

## What

Move Billing's IAM packages onto Corely.IAM 5.0.0 and Corely.IAM.Web 6.0.0, and every other package
to its latest version, so a host can run Billing beside IAM 5. A host on IAM 5 cannot load Billing's
IAM packages built against IAM 4: `AuthAction` moved from `Corely.IAM.Security.Constants` to
`Corely.IAM.Security.Enums`, and the old build looks for it in the old namespace.

## Packages

| Package | From | To |
|---|---|---|
| Corely.IAM | 4.0.1 | 5.0.0 |
| Corely.IAM.Web | 4.1.0 (5.0.0 in the WithIAM demo) | 6.0.0 |
| Corely.DataAccess | 3.2.0 | 3.2.1 |
| Corely.Common | 2.0.4 | 2.0.5 |
| Entity Framework Core and Microsoft.Extensions | 10.0.12 | 10.0.13 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | 10.0.13 |
| Microsoft.Extensions.TimeProvider.Testing | 10.10.0 | 10.11.0 |
| Microsoft.NET.Test.Sdk | 18.10.1 | 18.11.0 |
| System.CommandLine | 2.0.12 | 2.0.13 |

## As built

- **The only code change is the namespace:** `using Corely.IAM.Security.Constants` became
  `Corely.IAM.Security.Enums` in Billing.IAM, Billing.Web.IAM, their tests and the WithIAM demo. Nothing
  else Billing calls changed: `IAuthorizationProvider`, `IAuditProvider`, `RegisterResourceType`,
  `PermissionView` and `IBlazorUserContextAccessor` are as they were.
- **`Start-LocalStack.ps1` installs `corely-iam-db` 5.x** for the WithIAM demo, which must match
  IAM's major.
- **Versions follow the IAM 4 upgrade:** minor bumps for the two IAM packages, patches for the
  packages whose only change is their dependencies.
- **Tags are pushed one at a time:** GitHub creates no tag events when more than three tags are
  pushed at once (see `iam-4-and-ui-alignment.md`).
- **Checked in the browser** on the WithIAM demo: `olivia`, `carla`, `chloe` and `bobby` each see
  exactly what the demo's README says they should.
- **Seen, not caused by this:** leaving the usage dashboard while it is still loading can log
  `ObjectDisposedException` from its reference data query, because the torn down circuit disposes
  the scope under a query still in flight. The user has already left the page, so nothing shows.
  The dashboard code and IAM's permission lookup are unchanged by this upgrade.
