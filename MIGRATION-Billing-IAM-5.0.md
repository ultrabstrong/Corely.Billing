# Migrating to Corely.Billing.IAM 5.0

Account owners no longer hold Execute on `quota`, and registrations have their own resource type. A
permission covers what the caller chooses to do, never what the platform does as a result: settling
writes consumption, and consumption has no write methods because nobody should record usage by hand.
There is no database migration.

| Package | Version |
|---------|---------|
| Corely.Billing.IAM | 5.0.0 |
| Corely.Billing.Web.IAM | 6.0.0 |

## 1. Quota

`RegisterBillingResourceTypes()` gives owners Read on `quota`, not Read and Execute. Reserve, settle
and release still need Execute on `quota`, so a user running work no longer passes.

- **A host that meters as the system** changes nothing.
- **A host that meters under a signed in user** runs those calls as the system instead, or creates a
  permission with Execute on `quota` under system context and gives it to a role.

An owner default is a system defined row that IAM never edits, so an existing account keeps Execute
until its row is changed by hand: clear `Execute` on its `quota` permission.

## 2. Registrations

`registration` is a new resource type. Owners hold Read and Execute on it, and IAM adds that to
existing accounts at startup.

| Method | Was | Now |
|--------|-----|-----|
| `RegisterAsync`, `ReleaseRegistrationAsync`, `MoveRegistrationsAsync` | Execute on `quota` | Execute on `registration` |
| `ListRegistrationsAsync` | Read on `quota` | Read on `registration` |
| `GrantAction.ManageRegistrations` | Execute on `quota` | Execute on `registration` |

A role that managed registrations through `quota` needs the new permission. A host with API keys adds
`registration` to `ApiKeyResourceTypes` for a key that needs it.

## 3. The Billing API

`MapBillingApiEndpoints()` no longer maps reserve, settle and release. A host whose backend meters over
HTTP adds `MapBillingMeteringEndpoints()`.
