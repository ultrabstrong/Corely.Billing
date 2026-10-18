# GrantRequestForm

Asks for a grant: operation, unit, how many, and an optional note.

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
- Operation and unit offer the vocabulary's entries; a host with one of each sees a single choice in each.
- The requester is `IBillingAccountAccessor.GetUserIdAsync()`, null unless the host's accessor supplies it.
- "This account already has a pending request for that." is `AlreadyPendingError`. The list hides its request button when every operation and unit is pending, so this is only reached by a stale page.
- Dates are not asked for. The approver chooses the window; see [IGrantRequestService](../../../Corely.Billing/Docs/services/grant-request-service.md).
