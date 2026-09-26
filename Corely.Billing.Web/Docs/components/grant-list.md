# GrantList

An account's grants, newest first, each with its balance, validity window and status.

## Usage

```razor
<GrantList AccountId="accountId" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
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
- New, Edit and Delete each go through `IGrantActionGate`, with the row's grant id for Edit and Delete. A row the caller cannot update offers View instead of Edit. The host's decorators still refuse what the gate hides.
