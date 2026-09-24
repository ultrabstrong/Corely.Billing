# Routed Pages

Four pages built from the components. They route only when the host adds this assembly to its router, so a host that composes its own pages gets none of them.

| Route | Page | Shows |
|-------|------|-------|
| `/grants` | `GrantsPage` | `GrantList` |
| `/grants/new` | `GrantEditorPage` | `GrantEditor` for a new grant |
| `/grants/{id}` | `GrantEditorPage` | `GrantEditor` for that grant |
| `/usage` | `UsagePage` | `UsageDashboard` |

## Usage

Add the assembly in both places Blazor routes from:

```csharp
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(BillingWebRoutes).Assembly)
    .AddInteractiveServerRenderMode();
```

```razor
<Router AppAssembly="typeof(Routes).Assembly" AdditionalAssemblies="[typeof(BillingWebRoutes).Assembly]">
```

`BillingWebRoutes` holds the paths for links:

```razor
<NavLink href="@BillingWebRoutes.GRANTS">Grants</NavLink>
<NavLink href="@BillingWebRoutes.USAGE">Usage</NavLink>
```

## Notes

- The pages read the account and `CanManage` from `IBillingAccountAccessor`. With no account they say so.
- The pages carry no `[Authorize]` attribute. Guard them in the host's layout or router.
- They render `InteractiveServer` without prerendering.
