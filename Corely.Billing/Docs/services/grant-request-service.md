# IGrantRequestService

An account asks for a grant; someone allowed to decide approves or declines it. Approving creates an ordinary grant and links it to the request in one transaction.

## Methods

| Method | Parameters | Returns |
|--------|-----------|---------|
| `RequestGrantAsync` | `RequestGrantRequest` | `RequestGrantResult` |
| `GetGrantRequestAsync` | `Guid accountId, Guid grantRequestId` | `RetrieveSingleResult<GrantRequest>` |
| `ListGrantRequestsAsync` | `ListGrantRequestsRequest`, `IReadOnlySet<Guid>? authorizedResourceIds` | `RetrieveListResult<GrantRequest>` |
| `ApproveGrantRequestAsync` | `ApproveGrantRequestRequest` | `ApproveGrantRequestResult` |
| `DeclineGrantRequestAsync` | `DeclineGrantRequestRequest` | `DeclineGrantRequestResult` |

## Statuses

| Status | Means |
|--------|-------|
| `Pending` | Asked, not yet decided |
| `Approved` | A grant was created for it; `GrantId` names it |
| `Declined` | Refused; `DecisionReason` may say why |

A decided request never changes again.

## Usage

### Ask

```csharp
var result = await grantRequestService.RequestGrantAsync(new RequestGrantRequest(
    accountId, MyUsage.TextGeneration, Quantity: 500_000,
    Note: "Quarter end reports", RequestedByUserId: userId));
```

### Approve

```csharp
var result = await grantRequestService.ApproveGrantRequestAsync(new ApproveGrantRequestRequest(
    accountId, grantRequestId, ValidFromUtc: today, ValidToUtc: today.AddYears(1), DecidedByUserId: adminId));

var grantId = result.GrantId;
```

A request takes its kind and unit from the operation's declaration. The grant takes the request's operation, kind, unit, quantity and tags. The approver chooses only the window; change anything else afterwards through `IGrantService`.

### Decline

```csharp
await grantRequestService.DeclineGrantRequestAsync(new DeclineGrantRequestRequest(
    accountId, grantRequestId, Reason: "The annual plan covers this", DecidedByUserId: adminId));
```

### List pending

```csharp
var result = await grantRequestService.ListGrantRequestsAsync(new ListGrantRequestsRequest(
    [accountId], Status: GrantRequestStatus.Pending));
```

Without an order, pending requests list first, then newest `RequestedUtc` first.

## Validation

| Rule | Code |
|------|------|
| `AccountId` not empty | `ValidationError` |
| `Quantity` one or more, since a request is never for unlimited; null for a feature | `ValidationError` |
| `Note` and a decline's `Reason` up to 500 characters | `ValidationError` |
| Operation registered | `ValidationError` |
| Another request for the same account and operation still pending | `AlreadyPendingError` |
| An approval's window fails grant validation | `ValidationError`, and nothing is written |

## Notes

- Approve claims the request (`Pending` to `Approved`) and creates its grant inside one unit of work. Two approvals at once leave one grant: the second finds the request decided and returns `AlreadyDecidedError`
- Approve begins its own unit of work, so do not call it inside one the host already began
- One pending request per account and operation is checked by the library, not by a unique index; two requests made at the same instant can both land
- Every method is scoped by `accountId`; another account's request reads as `NotFoundError`
- Deleting the grant an approval created leaves the request's history, `GrantId` included
- Nothing here notifies anyone. A host that emails an admin when a request arrives, or the requester when it is decided, decorates `IGrantRequestService` through `BillingOptions.DecorateServices` and acts on a `Success` result
