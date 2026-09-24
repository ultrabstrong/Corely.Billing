# GrantEditor

A form that creates a grant, or edits one when given a `GrantId`.

## Usage

```razor
<GrantEditor AccountId="accountId" CanManage="true"
             OnSaved="id => Navigation.NavigateTo(BillingWebRoutes.GRANTS)"
             OnCancel="() => Navigation.NavigateTo(BillingWebRoutes.GRANTS)" />

<GrantEditor AccountId="accountId" GrantId="grantId" CanManage="canManage" OnSaved="ReloadAsync" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `GrantId` | `null` | The grant to edit; `null` creates |
| `CanManage` | `false` | Without it the form is read-only and has no save button |
| `OnSaved` | — | Called with the grant id after a successful save |
| `OnCancel` | — | Shows a Cancel button when set |

## Notes

- Operation and unit list the registered vocabulary by display name.
- An existing grant's operation cannot change; create a new grant instead.
- "Unlimited" saves a null quantity.
- Times are entered and shown in UTC.
