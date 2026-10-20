# Hosting the Components

The library ships components, not routed pages. The host writes a short page (a shell) per
component, at a path it chooses, and passes every link a component renders. Nothing here knows the
host's paths, so a host can put the account in them (`/accounts/<id>/grants`) or leave it out.

## A shell

`BillingPageBase` reads the account from `IBillingAccountAccessor` in the page's own scope, so a
shell does not repeat that code:

```razor
@page "/grants"
@inherits BillingPageBase
@rendermode @(new InteractiveServerRenderMode(prerender: false))

<PageTitle>Grants</PageTitle>

@if (Loaded && CurrentAccountId is null)
{
    <div class="alert alert-warning" role="alert">No account is selected.</div>
}
else if (CurrentAccountId is { } accountId)
{
    <GrantList AccountId="accountId"
               NewGrantHref="/grants/new"
               RequestGrantHref="/grants/request"
               GrantHref="id => $&quot;/grants/{id}&quot;" />
}
```

An editor or the request form navigates through its callbacks; nothing falls back to a library path:

```razor
<GrantEditor AccountId="accountId" GrantId="GrantId" OnSaved="BackToList" OnCancel="BackToList" />
<GrantRequestForm AccountId="accountId" OnSent="BackToList" OnCancel="BackToList" />
```

| Component | Links the host passes |
|-----------|-----------------------|
| `GrantList` | `NewGrantHref`, `RequestGrantHref`, `GrantHref` (all required) |
| `GrantEditor` | `OnSaved`, `OnCancel` callbacks |
| `GrantRequestForm` | `OnSent`, `OnCancel` callbacks |
| `UsageDashboard`, `UsageChart`, `ConsumptionTable` | none |

## Account paths with Corely.IAM

With Corely.IAM.Web, wrap the content in its `AccountScope`, which switches into the route's account
before anything renders. Load for `CurrentAccountId`, the signed in account, and use the route's
`AccountId` only to build links:

```razor
@page "/accounts/{AccountId:guid}/grants"
@attribute [Authorize]
@inherits BillingPageBase
@rendermode @(new InteractiveServerRenderMode(prerender: false))

<PageTitle>Grants</PageTitle>

<AccountScope AccountId="AccountId">
    @if (CurrentAccountId is { } accountId)
    {
        <GrantList AccountId="accountId"
                   NewGrantHref="@($"/accounts/{AccountId}/grants/new")"
                   RequestGrantHref="@($"/accounts/{AccountId}/grants/request")"
                   GrantHref="id => $&quot;/accounts/{AccountId}/grants/{id}&quot;" />
    }
</AccountScope>

@code {
    [Parameter] public Guid AccountId { get; set; }
}
```

## Working examples

- `Corely.Billing.Demos.Portal`: simple paths (`/grants`, `/usage`), no identity library.
- `Corely.Billing.Demos.WithIAM`: account paths with `AccountScope`.

## Notes

- Shells carry the host's `[Authorize]` and render `InteractiveServer` without prerendering.
- The router routes only the host's assembly; this one is never added to `AdditionalAssemblies`.