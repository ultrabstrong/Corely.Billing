# Migrating to Corely.Billing 5.0

5.0 lets a requester cancel their own grant request. Nothing in the database changes.

## What breaks

1. **`IGrantRequestService` has a new member,** `CancelGrantRequestAsync(CancelGrantRequestRequest,
   CancellationToken)`. A host that implements or decorates the interface adds it; a decorator passes
   it through, and a host that reacts to decisions can react to cancellations the same way.
2. **`GrantRequestStatus` has a new value,** `Cancelled`. A `switch` over the enum handles it.

## What follows the major

`Corely.Billing.IAM` 6.0, `Corely.Billing.Web` 6.0, `Corely.Billing.Web.IAM` 8.0 and the
`corely-billing-db` tool 5.0 take Corely.Billing 5.0 and change nothing else a host calls.
`Corely.Billing.Web`'s grant list now keeps decided requests visible as history, decides in a dialog,
and offers the requester a Cancel button; it needs no new parameters.
