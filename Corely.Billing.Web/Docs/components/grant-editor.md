# GrantEditor

A form that creates a grant, or edits one when given a `GrantId`. It asks only for what the operation's kind needs, and lists the seats held on a capacity grant.

## Usage

```razor
<GrantEditor AccountId="accountId"
             OnSaved="id => Navigation.NavigateTo(&quot;/grants&quot;)"
             OnCancel="() => Navigation.NavigateTo(&quot;/grants&quot;)" />

<GrantEditor AccountId="accountId" GrantId="grantId" OnSaved="ReloadAsync" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `GrantId` | `null` | The grant to edit; `null` creates |
| `OnSaved` | none | Called with the grant id after a successful save |
| `OnCancel` | none | Shows a Cancel button when set |

## The Form

1. **What to grant** lists the registered operations by display name, grouped by kind: Balance, Seats, Allowance, Feature.
2. The fields follow the kind. A balance or seats takes a quantity, or Unlimited, and a "Never expires" switch for the end date. An allowance takes the amount added, or Unlimited. A feature takes only its window.
3. The unit is never asked for. It comes from the operation, and shows beside the quantity.

## Registrations

An existing capacity grant shows its registrations under the form: each seat's reference, description, since when, and status (Active, Released, Grant ended, Grant disabled). Release, and "Move all to" another capacity grant of the same operation, go through `IGrantActionGate`'s `ManageRegistrations`.

## Notes

- Whether the form is editable is `IGrantActionGate`'s call: Create for a new grant, Update for an existing one. Without it a new grant shows a message, and an existing one shows the same form read-only.
- An editable existing grant also offers Disable grant, or Enable grant when it is disabled, under the same Update gate. It takes effect at once, without Save, and a disabled grant shows a note that nothing new can draw on it.
- An existing grant's operation and kind cannot change; create a new grant instead.
- "Unlimited" saves a null quantity; "Never expires" saves `GrantConstants.NEVER_EXPIRES_UTC`.
- Times are entered and shown in UTC.
