# GrantRequestForm

Asks for a grant: what to grant, how many, and an optional note.

## Usage

```razor
<GrantRequestForm AccountId="accountId" OnSent="BackToList" OnCancel="BackToList" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account asking |
| `OnSent` | none | Called with the new request's id |
| `OnCancel` | none | Shows Cancel when set |

## Notes

- The form renders only when `IGrantActionGate` allows `Request`; otherwise it says the caller may not request grants.
- What to grant lists the operations without a pending request, grouped by kind as in [GrantEditor](grant-editor.md). The unit comes from the operation; a feature asks for no amount.
- The requester is `IBillingAccountAccessor.GetUserIdAsync()`, null unless the host's accessor supplies it.
- "This account already has a pending request for that." is `AlreadyPendingError`. The list hides its request button when every operation is pending, so this is only reached by a stale page.
- Dates are not asked for. The approver chooses the window; see [IGrantRequestService](../../../Corely.Billing/Docs/services/grant-request-service.md).
