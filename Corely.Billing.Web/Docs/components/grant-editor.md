# GrantEditor

A form that creates a grant, or edits one when given a `GrantId`.

## Usage

```razor
<GrantEditor AccountId="accountId"
             OnSaved="id => Navigation.NavigateTo(BillingWebRoutes.GRANTS)"
             OnCancel="() => Navigation.NavigateTo(BillingWebRoutes.GRANTS)" />

<GrantEditor AccountId="accountId" GrantId="grantId" OnSaved="ReloadAsync" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `GrantId` | `null` | The grant to edit; `null` creates |
| `OnSaved` | — | Called with the grant id after a successful save |
| `OnCancel` | — | Shows a Cancel button when set |

## Notes

- Whether the form is editable is `IGrantActionGate`'s call: Create for a new grant, Update for an existing one. Without it a new grant shows a message, and an existing one shows the same form read-only.

- Operation and unit list the registered vocabulary by display name.
- An existing grant's operation cannot change; create a new grant instead.
- "Unlimited" saves a null quantity.
- Times are entered and shown in UTC.
