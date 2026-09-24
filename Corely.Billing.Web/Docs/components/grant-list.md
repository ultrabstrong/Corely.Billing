# GrantList

An account's grants, newest first, each with its balance, validity window and status.

## Usage

```razor
<GrantList AccountId="accountId" CanManage="canManage" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `CanManage` | `false` | Shows the create, edit and delete controls |
| `NewGrantHref` | `BillingWebRoutes.GRANT_NEW` | Where "New grant" goes |
| `GrantHref` | `BillingWebRoutes.GrantEditor` | Where a grant's edit link goes |
| `OnDeleted` | — | Called with the deleted grant |

## What Each Row Shows

- **Allowance** — "500 pages", or "Unlimited pages" for a null quantity
- **Balance** — a meter of used against quantity; running low under a tenth left, overdrawn past the quantity
- **Validity** — the window, with "Starts in", "Expires in" or "Expired … ago"
- **Status** — Upcoming, Active or Expired

## Notes

- Delete asks for confirmation in the row. Nothing opens a modal.
- `CanManage` hides controls; the host's decorators still refuse the call.
