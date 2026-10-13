# IAM 4 and UI alignment

**Status: done.** Released as Corely.Billing 2.1.1, Corely.Billing.Web 2.3.1, Corely.Billing.IAM
2.3.1, Corely.Billing.Web.IAM 2.2.1 and the migration CLI 2.0.3.

## As built

- Billing built on IAM 4 with no code changes.
- Two `Microsoft.Extensions` references in `Corely.Billing.csproj` are written across several lines
  and were missed by the first pass; left at 10.0.11 they failed restore with a package downgrade.
- The WithIAM demo did not load Bootstrap's script, which the user menu's dropdown needs; `App.razor`
  loads it now.
- The short grant ID is `Guid.ShortText()` in Billing.Web's extensions, tested directly.
- The tags for Web 2.3.0, IAM 2.3.0, Web.IAM 2.2.0 and CLI 2.0.2 were pushed in one `git push`.
  GitHub creates no tag events when more than three tags are pushed at once, so no release ran and
  nothing was published under those versions. Those tags were deleted; the packages released one
  patch later, each tag pushed on its own.

Corely.IAM 4.0.1 and Corely.IAM.Web 4.1.0 reworked the look of tables, status pills and IDs, and
moved IAM to a new major. Billing's components should read as part of the same product when a host
shows them beside IAM's pages, and Billing's IAM packages should run on IAM 4.

## Packages

| Package | From | To |
|---|---|---|
| Corely.IAM | 3.5.0 | 4.0.1 |
| Corely.IAM.Web | 3.5.1 | 4.1.0 |
| Corely.DataAccess | 3.0.0 | 3.2.0 |
| Corely.Common | 2.0.3 | 2.0.4 |
| Microsoft EF Core and Microsoft.Extensions | 10.0.11 | 10.0.12 |
| FluentValidation | 12.1.1 | 12.2.0 |
| System.CommandLine | 2.0.11 | 2.0.12 |
| Test tooling (xunit.v3, the test SDK, coverage, bunit, Moq, Testcontainers) | | latest patch or minor |

IAM 4's breaking changes (the password split, security settings from the platform account) touch
nothing Billing calls. Billing.IAM and Billing.Web.IAM take minor bumps for the new IAM dependency.

## Table headers

IAM's list headers are a quiet band: a neutral grey background (`#ebebeb` light, `#343a40` dark), a
1px line under it, and small muted capitals. Billing's two list headers, `.cbw-event-head` in
`ConsumptionTable` and `.cbw-grant-head` in `GrantList`, already have the capitals and the line. They
get the same background and color, with the dark values under `[data-bs-theme="dark"]`.

## Status pills

IAM's result pills are the soft Bootstrap variants (`bg-*-subtle` with `text-*-emphasis`) rather than
solid colors, so a column of them does not shout. `GrantStatusBadge` and `OutcomeBadge` move to the
soft variants with the same icons.

## Copyable IDs

IAM puts a small copy button beside every ID. `ConsumptionTable` shows the last eight characters of
the grant ID with the full one in a tooltip, which cannot be copied on a phone. A `CopyableId`
component in Billing.Web, shaped like IAM's (the text, then a copy button that turns into a check for
a moment), shows the short form and copies the full ID.

## WithIAM demo nav bar

IAM's hosts put the signed in user in a menu holding the account, the theme and sign out. The WithIAM
demo shows `user · account` as text with a separate Sign out button. It moves to the same user menu:
the account name as a header, then Sign out.

## Not changed

- Portal already hides the focus ring Blazor puts on the heading after navigating.
- Billing has no settings cards, audit log or detail pages of the kind IAM reworked.
