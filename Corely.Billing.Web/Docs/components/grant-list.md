# GrantList

An account's grants, newest first, each with its own state, validity window and status. Pending grant requests come first, in the same rows.

## Usage

```razor
<GrantList AccountId="accountId"
           NewGrantHref="/grants/new"
           RequestGrantHref="/grants/request"
           GrantHref="id => $&quot;/grants/{id}&quot;" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `NewGrantHref` | required | Where "New grant" goes |
| `RequestGrantHref` | required | Where "Request a grant" goes |
| `GrantHref` | required | Where a grant's edit link goes, given its id |
| `OnDeleted` | none | Called with the deleted grant |
| `ShowExport` | `true` | Show the CSV button |

## What Each Row Shows

| Column | A grant | A pending request |
|--------|---------|-------------------|
| Grant | "1,000,000 tokens", "Unlimited tokens", "7 users", "60 days", or the feature's name | What was asked for |
| State | By the grant's type, below | The note, or "No note" |
| Valid | The window, with "Starts in", "Expires in" or "Expired … ago" | "Requested today", "Requested 3 days ago" |
| Status | Upcoming, Active or Expired, or Disabled whatever the window | Requested |

Each grant type reads its own way in the State column:

| Type | Shows |
|------|-------|
| Balance | A meter of used against quantity: "320 of 500 pages left"; running low under a tenth left, overdrawn past the quantity |
| Unlimited | "Unlimited, 1,240 pages used" |
| Seats | "2 of 7 users in use" |
| Unlimited seats | "Unlimited, 12 in use" |
| Allowance | "+60 days, until 31 March 2026", or "with no end date" for a grant that never expires |
| Unlimited allowance | "Unlimited days, until 31 March 2026" |
| Feature | "Enabled, until 31 March 2026", or "Disabled" |

Only a balance draws a meter. The state comes with the grant from `ListGrantsAsync`, so the list makes no second call.

## Requests

- **Approve** confirms in the row, then approves for today to a year from today. A different window is set afterwards in the editor, on the grant it created.
- **Decline** opens an optional reason in the row, then Decline or Keep.
- Either refreshes the list: an approved request becomes its grant, a declined one disappears. A request someone else decided first reloads and says so.
- A caller who may not read requests sees grants alone, with no error.
- "Request a grant" sits in the toolbar and in the empty state, and is hidden while every operation already has a pending request.

## Notes

- Delete asks for confirmation in the row. Nothing opens a modal.
- New, Edit and Delete each go through `IGrantActionGate`, with the row's grant id for Edit and Delete. A row the caller cannot update offers View instead of Edit. "Request a grant" asks for `Request`; Approve and Decline ask for `Decide` with the request's id. The host's decorators still refuse what the gate hides.
- The decider is `IBillingAccountAccessor.GetUserIdAsync()`, null unless the host's accessor supplies it.
- CSV downloads every grant with its kind, type and state, and no requests. See [Export](../export.md).
