# Grant request history and controls

Found by the DocsToData owner on a rebuilt dev environment (2026-11-03): a platform admin declined a
request, it vanished from the Grants page, and the decline email never arrived (the host's mail was
still limited), so the requester had no way to learn it was declined or why.

## What is wrong today

1. **A decided request disappears from the page.** It is not deleted: the record stays with status
   `Declined` (or `Approved`) and its reason. `GrantList` only asks for
   `Status: GrantRequestStatus.Pending`, so everything decided drops out of sight. The reason a
   request was declined reaches the requester only by the host's email, which can fail or be missed.
2. **Declining happens inline in the row.** Clicking Decline swaps the row's buttons for a small
   reason box and a second Decline button. It is cramped, easy to misclick, and on a phone the row
   turns into a jumble.
3. **A requester can't withdraw a request.** Once asked, it waits for an admin even if the requester
   no longer needs it.

## The fix

1. **Decided requests stay visible.** The Grants page shows pending requests first, then the account's
   decided ones as history: outcome, who decided and when, and the reason for a decline or a
   cancellation. An approved request links to the grant it created. Decide how far back history goes
   (all of it, or a recent window with a way to see more) when building it; the records are already
   kept either way.
2. **Deciding happens in a dialog.** Decline opens a modal: what was asked, by whom and why, an
   optional reason, and what happens next (the requester is told, the request moves to history).
   Approve gets the same treatment, so both decisions look alike. It must work as cards on a phone.
3. **A requester can cancel their own pending request,** with an optional reason:
   1. A new status, `Cancelled`, and `CancelGrantRequestAsync` on `IGrantRequestService`, with its
      result code, telemetry, audit and authorization decorators like the rest.
   2. Authorization: the user who asked may cancel their own request; check whether an account owner
      may cancel anyone's on the account, and decide.
   3. Only a pending request can be cancelled; one already decided answers with a result code, not an
      exception.
   4. Cancelling uses the same dialog as deciding.
   5. A cancellation shows in history like a decline.
4. **Hosts are told.** A cancellation is a new event for hosts, as approve and decline are, so DocsToData
   can tell its staff the request was withdrawn. Note it in the migration guide for the version that
   ships this.

## Then, in DocsToData

Take the new version, and add the cancellation notice to `GrantRequestNotificationService` beside the
existing three, with its email template and tests.
