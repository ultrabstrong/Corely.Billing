# Usage Shapes

Every grant has a kind, and every way of using grants is one kind used one way. An app calls the quota read or write for its shape and never works with the storage underneath. The schema is the same for every shape, and a table with no rows costs nothing.

| Kind | The quantity is | The unit | Usage against it |
|------|-----------------|----------|------------------|
| `Consumable` | A balance, or null for unlimited | Required | Reserved and settled |
| `Capacity` | How many registrations it holds at once, or null for unlimited | Required | Registered and released |
| `Allowance` | An amount the app reads, or null for unlimited | Required | None |
| `Feature` | Always null | Always null | None: an active grant means enabled |

| Shape | Kind | What the app calls |
|-------|------|--------------------|
| Metered | Consumable | `ReserveAsync`, `SettleAsync` |
| Prepaid credits | Consumable, far future expiry | `GetBalanceAsync`, `SettleAsync` |
| Trial | Consumable, short window | `GetBalanceAsync` |
| Limited capacity | Capacity | `RegisterAsync`, `ReleaseRegistrationAsync`, `GetCapacityAsync` |
| Subscription | Capacity that never expires | `RegisterAsync` for members, `SetGrantDisabledAsync` for a lapse |
| An extra amount | Allowance | `GetAllowanceAsync` |
| Feature access | Feature | `HasFeatureAsync` |

Each operation declares its kind and unit when it is registered (see the [Usage Vocabulary](usage-vocabulary.md) docs), and a grant that disagrees with its operation is refused. Runnable examples are `Corely.Billing.Demos.Portal` (every kind), `Corely.Billing.Demos.Subscription`, and `Corely.Billing.Demos.WithIAM` (signed in through [Corely.IAM](https://github.com/ultrabstrong/Corely.IAM)) in the repository.

## Metered

Every part of the reservation pipeline. See the [Reservations](reservations.md) docs.

```csharp
using var scope = accessor.BeginScope(new OperationContext(correlationId, $"chat:{chatId}/turn:{turn}"));
await quotaService.ReserveAsync(new ReserveQuotaRequest(accountId, generation, token, maxTokens, "large-model"));
var reply = await GenerateAsync(prompt, maxTokens);
await quotaService.SettleAsync(new SettleQuotaRequest(accountId, generation, token, reply.TotalTokens));
```

## Prepaid Credits

Each purchase is a consumable grant with a far future expiry. Packs are spent soonest expiring first, so an older pack runs out before a newer one.

```csharp
await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, generation, GrantKind.Consumable, Quantity: 1_000, now, now.AddYears(10)));
```

`GetBalanceAsync` is what is left across active packs, null when one of them is unlimited. `SettleQuotaResult.RemainingRatio` gives the same after a charge, for a top-up prompt.

```csharp
var balance = await quotaService.GetBalanceAsync(accountId, generation);
if (balance.Remaining < 100)
    await PromptTopUpAsync(accountId);
```

## Trial

A small consumable grant with a short window. `HasAny` is false once the window ends or the balance runs out; `ReserveAsync` says which limit was hit.

```csharp
var reserved = await quotaService.ReserveAsync(new ReserveQuotaRequest(accountId, op, unit, 1, "app"));
var reason = reserved.ResultCode switch
{
    ReserveQuotaResultCode.NoGrantAvailableError => "The trial has ended",
    ReserveQuotaResultCode.InsufficientQuotaError => "The trial balance is used up",
    _ => null,
};
```

## Capacity

A capacity grant's quantity is how many registrations it holds at once: team members, active workflows, connected devices. A registration holds its place until it is released; nothing is used up.

```csharp
var registered = await quotaService.RegisterAsync(
    new RegisterRequest(accountId, teamMembers, Reference: $"user:{userId}", Description: userName));
if (registered.ResultCode == RegisterResultCode.NoCapacityError)
    return "The team is full";
```

- **One registration per reference**: a `Reference` already registered on an active grant of the operation is `AlreadyRegisteredError`.
- **Atomic**: two registrations racing for the last place cannot both succeed.
- **Longest lived first**: a registration goes on the active grant with room that ends last, so it does not sit on a grant ending next week.
- **Only active grants hold anything**: when a grant ends or is disabled, its registrations report `GrantEnded` or `GrantDisabled` and hold nothing, as if released. The reference can register again, and goes on an active grant like any other. Extending or enabling the grant brings its registrations back, so a reference that registered again meanwhile is then registered on both.
- **Never moved silently**: a new grant starts empty. `MoveRegistrationsAsync` releases registrations and registers them again on another grant, when the app asks.
- **Deleting a grant releases its registrations.**

`GetCapacityAsync` adds up the active grants: `Granted` (null for unlimited), `InUse` and `Free`.
## Subscription

A subscription is a capacity grant that never expires, with each member registered against it. The grant's end date is `Grant.NEVER_EXPIRES_UTC`, because a subscription ends when it lapses, not on a date. The app tracks whether it is paid: a lapse disables the grant, and a renewal enables it again, so its registrations come back with it.

```csharp
await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, members, GrantKind.Capacity, Quantity: 3, now, Grant.NEVER_EXPIRES_UTC));

await grantService.SetGrantDisabledAsync(new SetGrantDisabledRequest(accountId, grantId, Disabled: true));
```

Gate on capacity. A disabled grant is not active, so `Granted` is zero after a lapse.

```csharp
var capacity = await quotaService.GetCapacityAsync(accountId, members);
var open = capacity.ResultCode == QuotaReadResultCode.Success && capacity.Granted is null or > 0;
```

## An Extra Amount

An allowance is an amount the app reads and adds to its own default: extra retention days, a larger file size limit. Several active allowances add up, and an unlimited one makes `Total` null.

```csharp
var extra = await quotaService.GetAllowanceAsync(accountId, retention);
int? days = extra.Total is { } total ? defaultDays + (int)total : null;
```

## Feature Access

A feature grant has no quantity and no unit. The account has the feature while it holds an active one.

```csharp
var sftp = await quotaService.HasFeatureAsync(accountId, sftpAccess);
if (!sftp.HasFeature)
    return Forbid();
```

## Notes

- Each quota read refuses an operation of another kind with `WrongKindError`, so an allowance is never read as a feature or a balance.
- An unlimited consumable grant is spent before any limited grant it overlaps, and never runs short.
- `ListGrantsAsync` returns each grant with its own state: `Used` and `Remaining` for a consumable, `InUse` for a capacity.
- The Blazor components in [Corely.Billing.Web](../../Corely.Billing.Web/Docs/index.md) show each kind its own way: a meter for a balance, how much is registered for a capacity, the amount added for an allowance, enabled or disabled for a feature.
