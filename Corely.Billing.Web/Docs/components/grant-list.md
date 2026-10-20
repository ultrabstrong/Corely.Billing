# GrantList

An account's grants, newest first, each with its balance, validity window and status. Pending grant requests come first, in the same rows.

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
| Grant | "1,000,000 tokens", or "Unlimited tokens" for a null quantity | What was asked for |
| Balance | A meter of used against quantity; running low under a tenth left, overdrawn past the quantity. "Balance unavailable" when the caller cannot read consumption | The note, or "No note" |
| Valid | The window, with "Starts in", "Expires in" or "Expired … ago" | "Requested today", "Requested 3 days ago" |
| Status | Upcoming, Active or Expired, or Disabled whatever the window | Requested |

## Requests

- **Approve** confirms in the row, then approves for today to a year from today. A different window is set afterwards in the editor, on the grant it created.
- **Decline** opens an optional reason in the row, then Decline or Keep.
- Either refreshes the list: an approved request becomes its grant, a declined one disappears. A request someone else decided first reloads and says so.
- A caller who may not read requests sees grants alone, with no error.
- "Request a grant" sits in the toolbar and in the empty state, and is hidden while every operation and unit already has a pending request.

## Notes

- Delete asks for confirmation in the row. Nothing opens a modal.
- New, Edit and Delete each go through `IGrantActionGate`, with the row's grant id for Edit and Delete. A row the caller cannot update offers View instead of Edit. "Request a grant" asks for `Request`; Approve and Decline ask for `Decide` with the request's id. The host's decorators still refuse what the gate hides.
- The decider is `IBillingAccountAccessor.GetUserIdAsync()`, null unless the host's accessor supplies it.
- CSV downloads every grant with its used, remaining and overdrawn quantities, and no requests. See [Export](../export.md).
