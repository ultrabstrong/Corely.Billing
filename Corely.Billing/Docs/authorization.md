# Authorization

Corely.Billing makes no authorization decisions. It knows accounts only as ids, and nothing about users, roles or permissions. The host decides who may call what, by decorating the three public services.

## Features

- **Host-owned** — pair with Corely.IAM, or any other model
- **Service-level** — decorate `IGrantService`, `IQuotaService` and `IConsumptionService`; processors are internal
- **Result codes, not exceptions** — every result has an `UnauthorizedError` code for a decorator to return

## Usage

```csharp
internal class QuotaAuthorizationDecorator(IQuotaService inner, ICallerAccess access)
    : IQuotaService
{
    public async Task<ReserveQuotaResult> ReserveAsync(ReserveQuotaRequest request, CancellationToken ct = default) =>
        access.CanActFor(request.AccountId)
            ? await inner.ReserveAsync(request, ct)
            : new ReserveQuotaResult(ReserveQuotaResultCode.UnauthorizedError, "Not authorized");

    // ... the other members
}
```

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterOperation("document_extraction", "Document Extraction")
    .RegisterUnit("page", "page")
    .DecorateServices(services =>
    {
        services.Decorate<IGrantService, GrantAuthorizationDecorator>();
        services.Decorate<IQuotaService, QuotaAuthorizationDecorator>();
        services.Decorate<IConsumptionService, ConsumptionAuthorizationDecorator>();
    });
```

## Notes

- Host decorators run inside the telemetry decorators, so a denied call is still logged
- Every service method takes an `accountId`; check it against the caller rather than trusting it
- A headless worker settling quota for work it was handed usually needs a system context, not a user's
