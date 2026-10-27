using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Operations;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.IAM.Endpoints;
using Corely.IAM.Users.Providers;
using Corely.IAM.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Corely.Billing.Web.IAM.Extensions;

public static class BillingApiEndpointExtensions
{
    public const string DEFAULT_PREFIX = "/api/billing";
    private const string OPERATION = "/operations/{operation}";
    private const string OPERATION_ID_REQUIRED =
        "A non empty id the client chooses per unit of work, sent again to settle or release it";

    extension(IEndpointRouteBuilder endpoints)
    {
        public RouteGroupBuilder MapBillingApiEndpoints(string prefix = DEFAULT_PREFIX)
        {
            var group = endpoints
                .MapGroup(prefix)
                .RequireAuthorization()
                .AsIAMApi()
                .WithTags("Billing");

            group
                .MapGet($"{OPERATION}/balance", GetBalanceAsync)
                .WithName("GetBillingBalance")
                .WithSummary("What is left of the operation's consumable grants");
            group
                .MapGet($"{OPERATION}/allowance", GetAllowanceAsync)
                .WithName("GetBillingAllowance")
                .WithSummary("The operation's allowance");
            group
                .MapGet($"{OPERATION}/feature", HasFeatureAsync)
                .WithName("HasBillingFeature")
                .WithSummary("Whether the account holds the operation as a feature");
            group
                .MapGet($"{OPERATION}/capacity", GetCapacityAsync)
                .WithName("GetBillingCapacity")
                .WithSummary("The operation's capacity and how much of it is in use");
            group
                .MapPost($"{OPERATION}/reserve", ReserveAsync)
                .WithName("ReserveBillingQuota")
                .WithSummary("Reserve quota before doing the work");
            group
                .MapPost($"{OPERATION}/settle", SettleAsync)
                .WithName("SettleBillingQuota")
                .WithSummary("Settle a reservation with what the work actually used");
            group
                .MapPost($"{OPERATION}/release", ReleaseAsync)
                .WithName("ReleaseBillingQuota")
                .WithSummary("Release a reservation the work did not use");
            group
                .MapPost($"{OPERATION}/registrations", RegisterAsync)
                .WithName("RegisterBillingCapacity")
                .WithSummary("Take one unit of the operation's capacity");
            group
                .MapGet("/registrations", ListRegistrationsAsync)
                .WithName("ListBillingRegistrations")
                .WithSummary("The account's capacity registrations");
            group
                .MapDelete("/registrations/{registrationId:guid}", ReleaseRegistrationAsync)
                .WithName("ReleaseBillingRegistration")
                .WithSummary("Give a unit of capacity back");
            group
                .MapGet("/grants", ListGrantsAsync)
                .WithName("ListBillingGrants")
                .WithSummary("The account's grants the caller may read");
            group
                .MapGet("/grants/{grantId:guid}", GetGrantAsync)
                .WithName("GetBillingGrant")
                .WithSummary("One grant of the account");
            group
                .MapGet($"{OPERATION}/consumption/total", GetConsumptionTotalAsync)
                .WithName("GetBillingConsumptionTotal")
                .WithSummary("How much of a unit the operation consumed in a window");
            group
                .MapGet("/consumption/events", ListConsumptionEventsAsync)
                .WithName("ListBillingConsumptionEvents")
                .WithSummary("The account's consumption events, newest first");

            return group;
        }
    }

    private static async Task<IResult> GetBalanceAsync(
        string operation,
        IUserContextProvider context,
        IQuotaService quota,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                var result = await quota.GetBalanceAsync(accountId, op, ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> GetAllowanceAsync(
        string operation,
        IUserContextProvider context,
        IQuotaService quota,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                var result = await quota.GetAllowanceAsync(accountId, op, ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> HasFeatureAsync(
        string operation,
        IUserContextProvider context,
        IQuotaService quota,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                var result = await quota.HasFeatureAsync(accountId, op, ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> GetCapacityAsync(
        string operation,
        IUserContextProvider context,
        IQuotaService quota,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                var result = await quota.GetCapacityAsync(accountId, op, ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> ReserveAsync(
        string operation,
        ReserveQuotaBody body,
        IUserContextProvider context,
        IQuotaService quota,
        IOperationContextAccessor operations,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                if (
                    body.ToReserveQuotaRequest(accountId, op, context.GetUserContext()?.User?.Id)
                    is not { } request
                )
                    return InvalidToken(nameof(body.Unit));

                if (body.OperationId == Guid.Empty)
                    return Invalid(nameof(body.OperationId), OPERATION_ID_REQUIRED);

                using var scope = operations.BeginScope(body.OperationContext);
                var result = await quota.ReserveAsync(request, ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> SettleAsync(
        string operation,
        SettleQuotaBody body,
        IUserContextProvider context,
        IQuotaService quota,
        IOperationContextAccessor operations,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                if (body.ToSettleQuotaRequest(accountId, op) is not { } request)
                    return InvalidToken(nameof(body.Unit));

                if (body.OperationId == Guid.Empty)
                    return Invalid(nameof(body.OperationId), OPERATION_ID_REQUIRED);

                using var scope = operations.BeginScope(body.OperationContext);
                var result = await quota.SettleAsync(request, ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> ReleaseAsync(
        string operation,
        ReleaseQuotaBody body,
        IUserContextProvider context,
        IQuotaService quota,
        IOperationContextAccessor operations,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                if (body.ToReleaseQuotaRequest(accountId, op) is not { } request)
                    return InvalidToken(nameof(body.Unit));

                if (body.OperationId == Guid.Empty)
                    return Invalid(nameof(body.OperationId), OPERATION_ID_REQUIRED);

                using var scope = operations.BeginScope(body.OperationContext);
                var result = await quota.ReleaseAsync(request, ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> RegisterAsync(
        string operation,
        RegisterBody body,
        IUserContextProvider context,
        IQuotaService quota,
        CancellationToken ct
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                var result = await quota.RegisterAsync(body.ToRegisterRequest(accountId, op), ct);
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> ListRegistrationsAsync(
        IUserContextProvider context,
        IQuotaService quota,
        string? operation = null,
        Guid? grantId = null,
        bool includeReleased = false,
        int skip = 0,
        int take = 25,
        CancellationToken ct = default
    ) =>
        await WithOptionalOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                var result = await quota.ListRegistrationsAsync(
                    new ListRegistrationsRequest(
                        [accountId],
                        op,
                        grantId,
                        includeReleased,
                        skip,
                        take
                    ),
                    ct
                );
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> ReleaseRegistrationAsync(
        Guid registrationId,
        IUserContextProvider context,
        IQuotaService quota,
        CancellationToken ct
    )
    {
        if (context.CurrentAccountId is not { } accountId)
            return Results.Unauthorized();

        var result = await quota.ReleaseRegistrationAsync(accountId, registrationId, ct);
        return Results.Json(result, statusCode: result.ResultCode.StatusCode);
    }

    private static async Task<IResult> ListGrantsAsync(
        IUserContextProvider context,
        IGrantService grants,
        string? operation = null,
        GrantKind? kind = null,
        int skip = 0,
        int take = 25,
        CancellationToken ct = default
    ) =>
        await WithOptionalOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                var result = await grants.ListGrantsAsync(
                    new ListGrantsRequest([accountId], kind, op, Skip: skip, Take: take),
                    ct: ct
                );
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> GetGrantAsync(
        Guid grantId,
        IUserContextProvider context,
        IGrantService grants,
        CancellationToken ct
    )
    {
        if (context.CurrentAccountId is not { } accountId)
            return Results.Unauthorized();

        var result = await grants.GetGrantAsync(accountId, grantId, ct);
        return Results.Json(result, statusCode: result.ResultCode.StatusCode);
    }

    private static async Task<IResult> GetConsumptionTotalAsync(
        string operation,
        string unit,
        IUserContextProvider context,
        IConsumptionService consumption,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? provider = null,
        CancellationToken ct = default
    ) =>
        await WithOperationAsync(
            context,
            operation,
            async (accountId, op) =>
            {
                if (!UsageUnit.TryFrom(unit, out var usageUnit))
                    return InvalidToken(nameof(unit));

                var result = await consumption.GetConsumptionTotalAsync(
                    new GetConsumptionTotalRequest(
                        accountId,
                        op,
                        usageUnit,
                        fromUtc,
                        toUtc,
                        provider
                    ),
                    ct
                );
                return Results.Json(result, statusCode: result.ResultCode.StatusCode);
            }
        );

    private static async Task<IResult> ListConsumptionEventsAsync(
        IUserContextProvider context,
        IConsumptionService consumption,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 25,
        CancellationToken ct = default
    )
    {
        if (context.CurrentAccountId is not { } accountId)
            return Results.Unauthorized();

        var result = await consumption.ListConsumptionEventsAsync(
            new ListConsumptionEventsRequest([accountId], fromUtc, toUtc, Skip: skip, Take: take),
            ct
        );
        return Results.Json(result, statusCode: result.ResultCode.StatusCode);
    }

    private static async Task<IResult> WithOperationAsync(
        IUserContextProvider context,
        string operation,
        Func<Guid, UsageOperation, Task<IResult>> handle
    )
    {
        if (context.CurrentAccountId is not { } accountId)
            return Results.Unauthorized();
        if (!UsageOperation.TryFrom(operation, out var op))
            return InvalidToken(nameof(operation));
        return await handle(accountId, op);
    }

    private static async Task<IResult> WithOptionalOperationAsync(
        IUserContextProvider context,
        string? operation,
        Func<Guid, UsageOperation?, Task<IResult>> handle
    )
    {
        if (context.CurrentAccountId is not { } accountId)
            return Results.Unauthorized();
        if (operation is null)
            return await handle(accountId, null);
        if (!UsageOperation.TryFrom(operation, out var op))
            return InvalidToken(nameof(operation));
        return await handle(accountId, op);
    }

    private static IResult InvalidToken(string field) =>
        Invalid(
            field,
            $"Lowercase letters, digits and underscores, at most {UsageOperation.MAX_LENGTH} characters"
        );

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
