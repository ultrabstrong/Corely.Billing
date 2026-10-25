# Components

Every component takes the account as `AccountId` and calls the Billing services itself.

| Component | Does |
|-----------|------|
| [GrantList](grant-list.md) | Grants with their state, validity and status; edit and delete; pending requests to approve or decline |
| [GrantEditor](grant-editor.md) | Create or edit one grant, and the seats held on a capacity grant |
| [GrantRequestForm](grant-request-form.md) | Ask for a grant |
| [UsageChart](usage-chart.md) | Used per period, and each grant's live capacity |
| [ConsumptionTable](consumption-table.md) | Usage events, sorted and paged |
| [UsageDashboard](usage-dashboard.md) | Range, filters, chart and table together |

## Results

A component shows a service's error result as a message in place of its content:

| Result code | Shown |
|-------------|-------|
| `UnauthorizedError` | "You are not allowed to …" |
| `NotFoundError` | "This grant no longer exists." |
| `ValidationError` | The service's message |

## Refreshing

`GrantList`, `UsageChart`, `ConsumptionTable` and `UsageDashboard` expose `RefreshAsync()`. Take a `@ref` and call it after changing data elsewhere on the page:

```razor
<UsageDashboard @ref="_dashboard" AccountId="accountId" />

@code {
    private UsageDashboard? _dashboard;

    private async Task AfterWorkAsync() => await _dashboard!.RefreshAsync();
}
```
