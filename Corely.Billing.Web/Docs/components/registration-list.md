# RegistrationList

What is registered against one capacity operation: every registration across its grants, or those on one grant. Each row shows its reference, description, since when and status, with Release, and Move to another active grant of the operation.

## Usage

```razor
<RegistrationList AccountId="accountId" Operation="MyUsage.TeamMembers" />

<RegistrationList AccountId="accountId" Operation="grant.Operation" GrantId="grant.GrantId" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `Operation` | required | A capacity operation |
| `GrantId` | `null` | One grant's registrations; `null` lists every grant's |

## Notes

- Across grants, each row names its grant, and Move takes that one registration to the grant picked beside it. Scoped to one grant, "Move all to" moves every active registration on it at once.
- Status is `Active`, `Released`, `Grant ended` or `Grant disabled`. A registration on an ended or disabled grant holds nothing; move it, or register it again, to give it a place.
- Release and Move go through `IGrantActionGate`'s `ManageRegistrations`, with the registration's grant id. The host's decorators still refuse what the gate hides.
- Registering is the host's: references are its own ids, so it calls `IQuotaService.RegisterAsync` and then `RefreshAsync()` on the list.
- [GrantEditor](grant-editor.md) shows this list scoped to the capacity grant it edits.
