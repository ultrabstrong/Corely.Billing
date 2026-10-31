# Billing API

A host that serves an API maps Billing's endpoints beside IAM's token endpoints. A caller holding an
IAM API key exchanges it for a token, then reads quota, grants and consumption and manages
registrations over HTTP. The account is the token's, never a value in the request, and the decorators
from
[Corely.Billing.IAM](../../Corely.Billing.IAM/Docs/index.md) authorize each call against the caller's
permissions, as they do for the components.

```csharp
builder.Services.AddIAMWeb(options => options.AcceptBearerTokens = true);
builder.Services.AddBillingWebIam();

var app = builder.Build();
app.UseIAMApiResponses("/api");
app.UseIAMWebAuthentication();

app.MapIAMTokenEndpoints();
app.MapBillingApiEndpoints();
```

The token endpoints, renewal and the `401`/`403` answers are IAM's; see
[Bearer API](https://github.com/ultrabstrong/Corely.IAM/blob/master/Corely.IAM.Web/Docs/bearer-api.md).
Rate limits are the host's too: `MapBillingApiEndpoints()` returns its route group, so
`.RequireRateLimiting(policy)` applies one, as
[Rate limiting](https://github.com/ultrabstrong/Corely.IAM/blob/master/Corely.IAM.Web/Docs/bearer-api.md#rate-limiting)
shows. A key reaches only the resource types the platform's `ApiKeyResourceTypes` setting allows, so turn on
`quota`, `registration`, `consumption` and `grant` as the key needs them.

## Endpoints

Under `/api/billing`, or the prefix passed to `MapBillingApiEndpoints`. `{operation}` is a registered
operation, such as `text_generation`.

| Endpoint | Body or query | Calls |
|----------|---------------|-------|
| `GET operations/{operation}/balance` | | `IQuotaService.GetBalanceAsync` |
| `GET operations/{operation}/allowance` | | `GetAllowanceAsync` |
| `GET operations/{operation}/feature` | | `HasFeatureAsync` |
| `GET operations/{operation}/capacity` | | `GetCapacityAsync` |
| `POST operations/{operation}/registrations` | `{ "reference", "description" }`, both optional | `RegisterAsync` |
| `GET registrations` | `operation`, `grantId`, `includeReleased`, `skip`, `take` | `ListRegistrationsAsync` |
| `DELETE registrations/{registrationId}` | | `ReleaseRegistrationAsync` |
| `GET grants` | `operation`, `kind`, `skip`, `take` | `IGrantService.ListGrantsAsync` |
| `GET grants/{grantId}` | | `GetGrantAsync` |
| `GET operations/{operation}/consumption/total` | `unit`, `fromUtc`, `toUtc`, `provider` | `IConsumptionService.GetConsumptionTotalAsync` |
| `GET consumption/events` | `fromUtc`, `toUtc`, `skip`, `take` | `ListConsumptionEventsAsync` |

Creating, changing and deleting grants and deciding grant requests are not in the API; they stay in
the components.

## Metering

Reserving and settling writes consumption, so `MapBillingApiEndpoints()` leaves it out, and no
account holds Execute on `quota` by default. A host whose own backend meters over HTTP maps it
separately, and creates that permission itself under system context for the role its key holds.

```csharp
app.MapBillingMeteringEndpoints();
```

| Endpoint | Body | Calls |
|----------|------|-------|
| `POST operations/{operation}/reserve` | `{ "operationId", "unit", "quantity", "provider", "tags" }`, tags optional | `ReserveAsync` |
| `POST operations/{operation}/settle` | `{ "operationId", "unit", "actualQuantity" }` | `SettleAsync` |
| `POST operations/{operation}/release` | `{ "operationId", "unit" }` | `ReleaseAsync` |

`operationId` is a non empty id the client chooses for one unit of work. It is the
`OperationContext` the call runs in, so settle and release find the reservation made under the same
id. A missing one is a 400.

## Responses

Every response has the shape Corely.IAM.Web defines:
[Responses](https://github.com/ultrabstrong/Corely.IAM/blob/master/Corely.IAM.Web/Docs/bearer-api.md#responses).
`resultCode` and `message` are the service's, and `data` holds the rest of its result, or is null on a
failure. A list's page is `data` itself. The status follows the result code:

| Result code | Status |
|-------------|--------|
| `Success`, `Registered` | 200 |
| `ValidationError`, `WrongKindError`, a malformed operation or unit | 400 |
| `UnauthorizedError` | 403 |
| `NotFoundError` | 404 |
| `InsufficientQuotaError`, `NoGrantAvailableError`, `NotRecordedError`, `NoCapacityError`, `NoActiveGrantError`, `AlreadyRegisteredError` | 409 |

## Example

```http
POST /api/auth/apikey
{ "apiKey": "ciam_..." }

POST /api/billing/operations/text_generation/reserve
Authorization: Bearer <authToken>
{ "operationId": "0193a7c2-5d1e-7a40-9c2b-3f1e8d6a4b10", "unit": "token", "quantity": 4000, "provider": "openai" }

POST /api/billing/operations/text_generation/settle
Authorization: Bearer <authToken>
{ "operationId": "0193a7c2-5d1e-7a40-9c2b-3f1e8d6a4b10", "unit": "token", "actualQuantity": 3120 }
```

`Corely.Billing.Demos.WithIAM` serves both mappings, and `--seed` prints an API key holding the Client
role, which the seed gives Execute on `quota`.
