# Grant requests: an account asks, someone with the right permission decides

**Status: done.** Released as Corely.Billing 2.2.0, Corely.Billing.IAM 2.5.0, Corely.Billing.Web
2.5.0, Corely.Billing.Web.IAM 2.4.0 and Corely.Billing.DataAccessMigrations.Cli 2.1.0. What changed
while building is under "As built" at the end.

## Starting cold

Read, in this order:

1. This repository's `CLAUDE.md` and `DOCUMENTATION-STYLE.md`.
2. The grants domain as built: `Corely.Billing/Grants/` (entity, configuration, mapper, models,
   processor and its telemetry decorator, validator), `Services/IGrantService.cs`, `GrantService.cs`
   and `GrantServiceTelemetryDecorator.cs`, and `ServiceRegistrationExtensions.cs` for the
   registration order.
3. `Corely.Billing.IAM/` whole: the resource types, the grant authorization and audit decorators, and
   the two extension classes.
4. `Corely.Billing.Web/Components/GrantList.razor` and `.razor.cs`, `GrantEditor`, `IGrantActionGate`
   and both its implementations, and `Corely.Billing.Web/Docs/components/grant-list.md`.
5. `Corely.Billing.Demos.WithIAM/DemoSeed.cs` and its README, which this changes most.

The first consumer is DocsToData, whose plan `Plans/New/getting-started-and-grant-requests.md` (in
that repository) describes the host side: a Service Bus queue, a function and email. None of that
belongs here.

**Done when:** a request is made, approved and declined in the WithIAM demo, by users whose
permissions allow exactly that, at desktop and phone widths, light and dark; `RebuildAndTest.ps1`
and the provider matrix pass; docs are written; the owner has seen the demo; and only then are the
packages released and this plan moved to `Plans/Completed/` with an Outcome section.

## The problem

An account that needs more of something has no way to ask for it. A grant appears only when someone
who holds Create on `grant` makes one, and nothing records that the account wanted it, how much, or
why. Every host ends up building the same thing beside the library: a request record, a pending
state, and an approve step that creates the grant.

The approve step is the part a host gets wrong. Approving is two writes, a grant and the request's
new status. Done as two saves, a failure between them leaves a grant beside a request still marked
pending, and a second approve grants twice. Inside the library both rows live in `BillingDbContext`,
so they can commit together.

## What it is

A **grant request**: an account asks for a quantity of one unit for one operation, with an optional
note. It is pending until someone decides it. Approving creates an ordinary grant and links it;
declining records an optional reason. Nothing else changes state.

| Status | Means |
|---|---|
| `Pending` | Asked, not yet decided |
| `Approved` | A grant was created for it; `GrantId` names it |
| `Declined` | Refused; `DecisionReason` may say why |

No `Withdrawn` in this version. A requester who asked for the wrong amount asks an admin to decline
it. Recorded in `Feature-Ideas.md`.

## Deliverables

### 1. Core: the `GrantRequests` domain

Same layout as `Grants/`.

**Entity and table** `GrantRequests`:

| Column | Type | Notes |
|---|---|---|
| `GrantRequestId` | Guid | Version 7, library generated |
| `CreatedUtc` | DateTime | `IHasCreatedUtc` |
| `AccountId` | Guid | Indexed with `Status` |
| `Operation`, `Unit` | token | Stored as their tokens, like a grant |
| `Quantity` | long | Required, greater than zero. A request is never for unlimited |
| `Note` | string? | Up to 500 characters |
| `RequestedByUserId` | Guid? | Who asked, when the host passes it, as consumption's `UserId` does |
| `Status` | string | `Pending`, `Approved`, `Declined` |
| `DecidedUtc` | DateTime? | |
| `DecidedByUserId` | Guid? | |
| `DecisionReason` | string? | Up to 500 characters |
| `GrantId` | Guid? | The grant an approval created. No foreign key: deleting that grant later leaves the request's history intact |
| `TagsJson` | string? | As grants carry, for a host's own references |

**`IGrantRequestService`**, public, beside `IGrantService`:

| Method | Request | Returns |
|---|---|---|
| `RequestGrantAsync` | `RequestGrantRequest(AccountId, Operation, Unit, Quantity, Note?, RequestedByUserId?, Tags?)` | `RequestGrantResult(ResultCode, Message, CreatedId)` |
| `GetGrantRequestAsync` | `Guid accountId, Guid grantRequestId` | `RetrieveSingleResult<GrantRequest>` |
| `ListGrantRequestsAsync` | `ListGrantRequestsRequest(AccountId, Filter?, Order?, Skip, Take)`, `authorizedResourceIds` | `RetrieveListResult<GrantRequest>` |
| `ApproveGrantRequestAsync` | `ApproveGrantRequestRequest(AccountId, GrantRequestId, ValidFromUtc, ValidToUtc, DecidedByUserId?)` | `ApproveGrantRequestResult(ResultCode, Message, GrantId)` |
| `DeclineGrantRequestAsync` | `DeclineGrantRequestRequest(AccountId, GrantRequestId, Reason?, DecidedByUserId?)` | `DeclineGrantRequestResult(ResultCode, Message)` |

Lists default to pending first, then newest first.

**Result codes.** `RequestGrantResultCode`: `Success`, `ValidationError`, `AlreadyPendingError`,
`UnauthorizedError`. Approve and decline share `DecideGrantRequestResultCode`: `Success`,
`NotFoundError`, `AlreadyDecidedError`, `ValidationError`, `UnauthorizedError`.

**Rules.**

1. **One pending request per account, operation and unit.** A second gets `AlreadyPendingError`.
   Checked in the processor, not by a unique index: the index this needs is filtered (pending rows
   only), which SQL Server supports and MySQL does not. Two simultaneous requests can both land;
   that is harmless, since each still needs a decision.
2. **Approve is one transaction.** Inside `IUnitOfWorkProvider`: claim the request with
   `ExecuteUpdateAsync` where its id, account and `Pending` status match, setting `Approved`, the
   decider and the time; zero rows means `NotFoundError` or `AlreadyDecidedError` (read once to tell
   which) and nothing is written. Then create the grant through `IGrantProcessor` (so grant
   validation and grant telemetry apply), set `GrantId`, commit. A grant validation failure rolls
   the claim back and returns `ValidationError`. Two admins approving at once: the second claim
   matches no row. The grant's quantity, operation and unit are the request's; the approver chooses
   only the window.
3. **Decline** is the same claim with `Declined` and the reason, and no grant.
4. **Every method is scoped by `accountId`**, as grants are; another account's request reads as
   `NotFoundError`.
5. **A host's own unit of work.** `BeginAsync` throws when one is already active, as in Corely.IAM's
   `RegistrationService`. A host must not wrap approve in its own; documented in the service doc.

**No hook for the host.** A host that wants to publish a message or send an email when a request is
made or decided decorates `IGrantRequestService` through `BillingOptions.DecorateServices`, exactly
as authorization does, and acts on a `Success` result. DocsToData does that; nothing here knows it.

**Plumbing**, mirroring grants: `GrantRequestProcessor` with `GrantRequestProcessorTelemetryDecorator`
(metrics `grant_request.created`, `grant_request.approved`, `grant_request.declined`, named in
`BillingMetricNames`), `GrantRequestService` with `GrantRequestServiceTelemetryDecorator`, a
validator, a mapper with its own tests, registration in the same order as grants (host decorators
between the service and its telemetry decorator).

**Migration** `AddGrantRequestsAndDisabledGrants` in both providers through `AddMigration.ps1`, one
for this release: the `GrantRequests` table, and the `Disabled` column on `Grants` (section 3a),
defaulting to false so every existing grant stays enabled.

### 2. Corely.Billing.IAM: a `grant_request` resource type

| Action | Means | Account owner holds it by default |
|---|---|---|
| Read | See the account's requests | Yes |
| Create | Ask | Yes |
| Update | Approve or decline | No |

`RegisterBillingResourceTypes()` registers it with owner actions Read and Create, so every account
owner can ask without any setup, and nobody can decide until a role is given Update.

**Approve also needs Create on `grant`**, because it creates one; checking only `grant_request` would
let a role that cannot create grants create them by approving. Decline needs only Update on
`grant_request`. `GrantRequestAuthorizationDecorator` and `GrantRequestAuditDecorator` follow the
grant ones line for line, and `UseCorelyIamPermissions()` registers both.

### 3. Corely.Billing.Web: pending requests in the grant list, and a request form

**`GrantList` shows pending requests above its grants, in the same rows.** Same columns, so a phone
gets the same card:

| Column | A pending request shows |
|---|---|
| Grant | "10,000 tokens" and the operation, as a grant row |
| Balance | The note, or "No note" |
| Valid | "Requested 2 days ago" |
| Status | A "Requested" badge |
| Actions | Approve and Decline, for a caller who may decide |

- **Approve** confirms in the row ("Approve 10,000 tokens for a year from today?"), the way Delete
  confirms. The window is today to a year from today, the editor's own default for a new grant; a
  different window is set afterwards in the editor, on the grant it created.
- **Decline** opens a reason box in the row, optional, then Decline or Keep.
- Both refresh the list. An approved request disappears and its grant appears; a declined one
  disappears.
- **A caller without Read on `grant_request` sees no request rows** and no error, as a caller without
  Read on consumption sees "Balance unavailable" rather than a failure.
- **The toolbar and the empty state offer "Request a grant"** to a caller who may ask, and "New
  grant" to one who may create, independently. A caller with both sees both. With a pending request
  for every operation and unit the vocabulary has, the request button goes; this is the "a second
  click does not send a second request" rule, read from the rows already loaded.
- New parameter `RequestGrantHref`, default `BillingWebRoutes.GRANT_REQUEST`.

**`GrantRequestForm`**, a component: operation, unit (both from the vocabulary, as the editor offers
them), quantity, note, Send. On `Success` it raises `OnSent`; `AlreadyPendingError` shows "This
account already has a pending request for that." The routed page `/grants/request`
(`GrantRequestPage`) wraps it the way `GrantEditorPage` wraps the editor, returning to the list.

**The gate grows two actions.** `GrantAction.Request` and `GrantAction.Decide`, so the components
still ask one interface. `OpenGrantActionGate` allows both. In Corely.Billing.Web.IAM,
`PermissionViewGrantActionGate` maps `Request` to Create on `grant_request`, and `Decide` to Update on
`grant_request` nested inside Create on `grant`, the same pair approve is authorized against. Decline
shows under `Decide` too: one gate for both buttons, and the service still lets a caller with only
Update decline through the API.

**The requester's name is not shown.** Billing has user ids, not names. A host that wants "asked by
Olivia" composes its own row; recorded in `Feature-Ideas.md` as a name resolver parameter if a second
host wants it.

### 3a. Disabling a grant

Added to this release at the owner's request, since it already carries a migration. A grant can be
paused without deleting it: a payment problem, a suspected abuse, anything a host wants to stop
without losing the grant or its history.

- **`Grant.Disabled`**, a column defaulting to false, so every existing grant stays enabled.
- **No new allowances.** A disabled grant is not chosen for a reservation and does not count toward
  availability, exactly as an expired one is not.
- **Work in flight finishes.** Settling re-splits the final quantity across the account's live
  grants. A disabled grant stays in that split for an operation that already holds quota on it, so
  the work that was admitted settles where it was held. Releasing never looked at grants.
- **`IGrantService.SetGrantDisabledAsync(SetGrantDisabledRequest(AccountId, GrantId, Disabled))`**,
  returning `ModifyResult`. A method of its own, not a field on `UpdateGrantRequest`: an update that
  forgot the field would silently re-enable a grant. Authorized as Update on `grant`, that grant, and
  audited as an update.
- **Telemetry:** `entitlements.grant.disabled` and `entitlements.grant.enabled`.
- **Web:** a Disabled status that outranks the window (Upcoming, Active, Expired), a badge for it,
  the CSV's `status` column reads `disabled`, and the editor offers Disable grant or Enable grant
  under the same Update gate as Save.

Tests: a disabled grant is skipped by reserve and availability; a hold taken before disabling still
settles onto that grant; enabling restores it; the IAM decorator checks Update on that grant; the
status reading and the editor toggle. Integration: the column round trips and the composed balance
after a disable mid-operation reads as an invoice would.

### 4. Demos

- **WithIAM:** a fifth user, `pat`, with a "Provisioner" role: Read and Create on `grant`, Read and
  Update on `grant_request`, Read on `consumption`. The stand-in for a platform admin. The seed adds
  one pending request from `olivia` so the list shows one on first open. The README's table gains
  `pat` and what each user sees of requests: `olivia` asks and sees hers pending; `carla` sees them
  (if given Read; decided while building) but cannot decide; `pat` approves and declines.
- **Portal:** the open gate shows everything, so the seed adds one pending request and the demo
  shows request, approve and decline with no sign in.
- `Docs/demos.md` follows.

### 5. Tests

- **Unit, core:** the validator; the mapper; the processor's rules on the mock repositories (one
  pending per account, operation and unit; approve creates the grant with the request's quantity,
  operation and unit and links it; approve and decline of a decided request; a grant validation
  failure on approve); both telemetry decorators.
- **Unit, IAM:** each method authorized against the right action, approve against both types,
  decline against one; the audit decorator records each call; the resource type registers with Read
  and Create for owners.
- **Unit, Web (bUnit):** request rows render beside grants; no rows and no error without Read;
  Approve confirms then calls approve with today to a year; Decline sends its reason; the request
  button hides when every operation and unit is pending; the form's result codes; the IAM gate's
  mapping of the two new actions.
- **Integration:** the table and its round trip; approve leaves exactly one grant and an `Approved`
  request on SQLite, and two approves of one request leave one grant (the second returns
  `AlreadyDecidedError`); a grant validation failure on approve leaves the request pending and no
  grant. The provider matrix runs the migration on SQL Server and MySQL.
- **Functional:** both demos still start and serve `/grants/request`.

Regression proof, per `CLAUDE.md`: break the claim (drop the `Pending` condition) and watch the double
approve test fail; break the gate mapping and watch its test fail.

### 6. Docs

- `Corely.Billing/Docs/services/grant-request-service.md`, new; `services/index.md`, `result-codes.md`,
  `telemetry.md`, `authorization.md` and `architecture.md` follow.
- `Corely.Billing.IAM/Docs/index.md`: the new resource type and why approve checks two.
- `Corely.Billing.Web/Docs/components/grant-list.md`: request rows, the two actions, the new
  parameter. `grant-request-form.md`, new. `pages.md`: the new route.
- `Corely.Billing.Web.IAM/Docs/index.md`: the two new gate mappings.

### 7. Release

Only after the owner has seen the demo, and only on the owner's say-so, per `CLAUDE.md`. New public
API and an additive schema change, so minor versions:

| Package | Now | Next |
|---|---|---|
| `Corely.Billing` | 2.1.2 | 2.2.0 |
| `Corely.Billing.DataAccessMigrations.Cli` | 2.0.4 | 2.1.0, for the migration |
| `Corely.Billing.IAM` | 2.4.1 | 2.5.0 |
| `Corely.Billing.Web` | 2.4.3 | 2.5.0 |
| `Corely.Billing.Web.IAM` | 2.3.1 | 2.4.0 |

DocsToData takes them afterwards, in its own repository, with its own plan.

## Out of scope

- Withdrawing a request.
- Any notification. Hosts decorate the service.
- A list of requests across accounts. A platform admin enters the account, as for grants.
- Editing the grant before approving. Approve creates it; the editor changes it after.
- Prices, payment, or anything that makes a request a purchase.

## Decided while planning

The owner gave the go-ahead to build without a round of questions; these are the calls made, for the
owner to overturn on review.

1. **Requests live in Corely.Billing**, so approve commits the request and the grant together and the
   grant list can show requests in its own rows. Settled with the owner in DocsToData's planning.
2. **`Approved`, not `Fulfilled`,** to match the button.
3. **One pending per account, operation and unit,** not per account: a library host may meter
   several things, and one pending request for tokens should not block one for images.
4. **Checked in the processor, no unique index,** because MySQL has no filtered index.
5. **Approve also requires Create on `grant`.**
6. **No withdraw, no requester names, no cross account list** in this version.
7. **Approve takes only the window.** Quantity, operation and unit come from the request.

## As built

Built as planned, with these differences:

- **Disabling a grant joined the release** (section 3a), at the owner's request, so the release has
  one migration: `AddGrantRequestsAndDisabledGrants`.
- **`IBillingAccountAccessor.GetUserIdAsync()`**, a default interface member returning null, so the
  request form and the list can record who asked and who decided without a parameter on every
  component. Corely.Billing.Web.IAM's accessor answers it from the signed-in user.
- **The provisioner in the WithIAM demo is `priya`,** not `pat`: IAM refuses usernames under five
  characters. `carla` was given nothing on `grant_request`, so she sees no requests.
- **Corely.IAM already backfills owner defaults** for a newly registered resource type on existing
  accounts, so accounts made before `grant_request` existed gain Read and Create on it at startup.
- **Who asked and who decided come from IAM, not the caller.** A security review of the first push
  found `RequestedByUserId` and `DecidedByUserId` were taken as given, so a caller could attribute a
  request or decision to someone else. `GrantRequestAuthorizationDecorator` now replaces both with
  the signed-in user's id; only the system context keeps the id it was passed. Fixed before tagging.
- **The request form's quantity starts empty,** not at a red zero, found clicking through.
- **Regression proofs:** dropping the `Pending` condition from the claim failed the late approval
  test (two grants instead of one); dropping Create on `grant` from the `Decide` mapping failed two
  gate tests; dropping the held grant exception from the live grant query failed the in flight
  settle test (the work moved to another grant).

Checked: the full suite and the provider matrix on SQL Server and MySQL; the WithIAM demo clicked
through as `olivia` (request) and `priya` (approve, then decline) at desktop and phone widths, light
and dark; the Portal demo's disable and enable, with a new image job refused while the only image
grant was disabled.
