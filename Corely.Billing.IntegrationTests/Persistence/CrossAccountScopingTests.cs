using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Corely.Billing.Consumption.Enums;
using Corely.Billing.Consumption.Models;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

public sealed class CrossAccountScopingTests : IAsyncLifetime
{
    private const long VICTIM_GRANT_QUANTITY = 913_579;
    private const long VICTIM_REQUEST_QUANTITY = 424_242;
    private const long VICTIM_CONSUMED = 4_871;
    private const string VICTIM_PROVIDER = "victim-provider";
    private const string VICTIM_SETTLED_SCOPE = "victim-settled";
    private const string VICTIM_OPEN_SCOPE = "victim-open";

    private static readonly Guid Attacker = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Victim = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly JsonSerializerOptions Json = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    private readonly BillingTestHost _host = new();
    private Guid _victimGrantId;
    private Guid _victimCapacityGrantId;
    private Guid _victimRequestId;
    private Guid _victimRegistrationId;
    private Guid _attackerCapacityGrantId;
    private DateTime _now;

    public async ValueTask InitializeAsync()
    {
        _now = _host.TimeProvider.GetUtcNow().UtcDateTime;

        _victimGrantId = await CreateGrantAsync(
            Victim,
            TestUsage.Extraction,
            GrantKind.Consumable,
            VICTIM_GRANT_QUANTITY
        );
        _victimCapacityGrantId = await CreateGrantAsync(
            Victim,
            TestUsage.Members,
            GrantKind.Capacity,
            3
        );
        await CreateGrantAsync(Victim, TestUsage.Sftp, GrantKind.Feature, null);
        await CreateGrantAsync(Victim, TestUsage.Retention, GrantKind.Allowance, 30);
        _attackerCapacityGrantId = await CreateGrantAsync(
            Attacker,
            TestUsage.Members,
            GrantKind.Capacity,
            3
        );

        _victimRequestId = (
            await Grants<IGrantRequestService, RequestGrantResult>(s =>
                s.RequestGrantAsync(
                    new RequestGrantRequest(
                        Victim,
                        TestUsage.Extraction,
                        VICTIM_REQUEST_QUANTITY,
                        "victim request"
                    )
                )
            )
        ).CreatedId;

        var registration = await Grants<IQuotaService, RegisterResult>(s =>
            s.RegisterAsync(new RegisterRequest(Victim, TestUsage.Members, "victim-ref"))
        );
        Assert.Equal(RegisterResultCode.Registered, registration.ResultCode);
        _victimRegistrationId = registration.Registration!.RegistrationId;

        await InOperationAsync(
            VICTIM_SETTLED_SCOPE,
            s =>
                s.ReserveAsync(
                    new ReserveQuotaRequest(
                        Victim,
                        TestUsage.Extraction,
                        TestUsage.Page,
                        1,
                        VICTIM_PROVIDER
                    )
                )
        );
        await InOperationAsync(
            VICTIM_SETTLED_SCOPE,
            s =>
                s.SettleAsync(
                    new SettleQuotaRequest(
                        Victim,
                        TestUsage.Extraction,
                        TestUsage.Page,
                        VICTIM_CONSUMED
                    )
                )
        );
        await InOperationAsync(
            VICTIM_OPEN_SCOPE,
            s =>
                s.ReserveAsync(
                    new ReserveQuotaRequest(
                        Victim,
                        TestUsage.Extraction,
                        TestUsage.Page,
                        61,
                        VICTIM_PROVIDER
                    )
                )
        );
    }

    public ValueTask DisposeAsync()
    {
        _host.Dispose();
        return ValueTask.CompletedTask;
    }

    private Dictionary<string, Func<Task<object?>>> Cases() =>
        new()
        {
            ["IGrantService.CreateGrantAsync"] = () =>
                Call<IGrantService>(s =>
                    s.CreateGrantAsync(
                        new CreateGrantRequest(
                            Attacker,
                            TestUsage.Extraction,
                            GrantKind.Consumable,
                            5,
                            _now,
                            _now.AddDays(1)
                        )
                    )
                ),
            ["IGrantService.GetGrantAsync"] = () =>
                Call<IGrantService>(s => s.GetGrantAsync(Attacker, _victimGrantId)),
            ["IGrantService.ListGrantsAsync"] = () =>
                Call<IGrantService>(s => s.ListGrantsAsync(new ListGrantsRequest([Attacker]))),
            ["IGrantService.UpdateGrantAsync"] = () =>
                Call<IGrantService>(s =>
                    s.UpdateGrantAsync(
                        new UpdateGrantRequest(Attacker, _victimGrantId, 1, _now, _now.AddDays(1))
                    )
                ),
            ["IGrantService.SetGrantDisabledAsync"] = () =>
                Call<IGrantService>(s =>
                    s.SetGrantDisabledAsync(
                        new SetGrantDisabledRequest(Attacker, _victimGrantId, true)
                    )
                ),
            ["IGrantService.DeleteGrantAsync"] = () =>
                Call<IGrantService>(s => s.DeleteGrantAsync(Attacker, _victimGrantId)),
            ["IGrantRequestService.RequestGrantAsync"] = () =>
                Call<IGrantRequestService>(s =>
                    s.RequestGrantAsync(new RequestGrantRequest(Attacker, TestUsage.Extraction, 5))
                ),
            ["IGrantRequestService.GetGrantRequestAsync"] = () =>
                Call<IGrantRequestService>(s => s.GetGrantRequestAsync(Attacker, _victimRequestId)),
            ["IGrantRequestService.ListGrantRequestsAsync"] = () =>
                Call<IGrantRequestService>(s =>
                    s.ListGrantRequestsAsync(new ListGrantRequestsRequest([Attacker]))
                ),
            ["IGrantRequestService.ApproveGrantRequestAsync"] = () =>
                Call<IGrantRequestService>(s =>
                    s.ApproveGrantRequestAsync(
                        new ApproveGrantRequestRequest(
                            Attacker,
                            _victimRequestId,
                            _now,
                            _now.AddDays(30)
                        )
                    )
                ),
            ["IGrantRequestService.DeclineGrantRequestAsync"] = () =>
                Call<IGrantRequestService>(s =>
                    s.DeclineGrantRequestAsync(
                        new DeclineGrantRequestRequest(Attacker, _victimRequestId)
                    )
                ),
            ["IQuotaService.GetBalanceAsync"] = () =>
                Call<IQuotaService>(s => s.GetBalanceAsync(Attacker, TestUsage.Extraction)),
            ["IQuotaService.ReserveAsync"] = () =>
                InOperationAsync(
                    VICTIM_OPEN_SCOPE,
                    s =>
                        s.ReserveAsync(
                            new ReserveQuotaRequest(
                                Attacker,
                                TestUsage.Extraction,
                                TestUsage.Page,
                                1,
                                "attacker"
                            )
                        )
                ),
            ["IQuotaService.SettleAsync"] = () =>
                InOperationAsync(
                    VICTIM_OPEN_SCOPE,
                    s =>
                        s.SettleAsync(
                            new SettleQuotaRequest(
                                Attacker,
                                TestUsage.Extraction,
                                TestUsage.Page,
                                1
                            )
                        )
                ),
            ["IQuotaService.ReleaseAsync"] = () =>
                InOperationAsync(
                    VICTIM_OPEN_SCOPE,
                    s =>
                        s.ReleaseAsync(
                            new ReleaseQuotaRequest(Attacker, TestUsage.Extraction, TestUsage.Page)
                        )
                ),
            ["IQuotaService.GetAllowanceAsync"] = () =>
                Call<IQuotaService>(s => s.GetAllowanceAsync(Attacker, TestUsage.Retention)),
            ["IQuotaService.HasFeatureAsync"] = () =>
                Call<IQuotaService>(s => s.HasFeatureAsync(Attacker, TestUsage.Sftp)),
            ["IQuotaService.GetCapacityAsync"] = () =>
                Call<IQuotaService>(s => s.GetCapacityAsync(Attacker, TestUsage.Members)),
            ["IQuotaService.RegisterAsync"] = () =>
                Call<IQuotaService>(s =>
                    s.RegisterAsync(new RegisterRequest(Attacker, TestUsage.Members, "victim-ref"))
                ),
            ["IQuotaService.ReleaseRegistrationAsync"] = () =>
                Call<IQuotaService>(s =>
                    s.ReleaseRegistrationAsync(Attacker, _victimRegistrationId)
                ),
            ["IQuotaService.MoveRegistrationsAsync"] = () =>
                Call<IQuotaService>(s =>
                    s.MoveRegistrationsAsync(
                        new MoveRegistrationsRequest(
                            Attacker,
                            _victimCapacityGrantId,
                            _attackerCapacityGrantId,
                            [_victimRegistrationId]
                        )
                    )
                ),
            ["IQuotaService.ListRegistrationsAsync"] = () =>
                Call<IQuotaService>(s =>
                    s.ListRegistrationsAsync(
                        new ListRegistrationsRequest(
                            [Attacker],
                            GrantId: _victimCapacityGrantId,
                            IncludeReleased: true
                        )
                    )
                ),
            ["IConsumptionService.GetConsumptionTotalAsync"] = () =>
                Call<IConsumptionService>(s =>
                    s.GetConsumptionTotalAsync(
                        new GetConsumptionTotalRequest(
                            Attacker,
                            TestUsage.Extraction,
                            TestUsage.Page
                        )
                    )
                ),
            ["IConsumptionService.GetConsumptionTimeSeriesAsync"] = () =>
                Call<IConsumptionService>(s =>
                    s.GetConsumptionTimeSeriesAsync(
                        new GetConsumptionTimeSeriesRequest(
                            Attacker,
                            _now.AddDays(-30),
                            _now.AddDays(1),
                            TimeBucket.Day,
                            GrantIds: [_victimGrantId]
                        )
                    )
                ),
            ["IConsumptionService.ListConsumptionEventsAsync"] = () =>
                Call<IConsumptionService>(s =>
                    s.ListConsumptionEventsAsync(
                        new ListConsumptionEventsRequest([Attacker], GrantIds: [_victimGrantId])
                    )
                ),
            ["IConsumptionService.ListProvidersAsync"] = () =>
                Call<IConsumptionService>(s => s.ListProvidersAsync(Attacker)),
            ["IConsumptionService.GetEarliestConsumptionAsync"] = () =>
                Call<IConsumptionService>(s => s.GetEarliestConsumptionAsync(Attacker)),
            ["IConsumptionService.CountAbandonedReservationsAsync"] = () =>
                Call<IConsumptionService>(s => s.CountAbandonedReservationsAsync(Attacker)),
        };

    public static TheoryData<string> CaseNames() =>
        [
            .. ServiceMethods()
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .Order(StringComparer.Ordinal),
        ];

    private static IEnumerable<MethodInfo> ServiceMethods() =>
        typeof(IGrantService)
            .Assembly.GetExportedTypes()
            .Where(t =>
                t.IsInterface
                && t.Namespace == typeof(IGrantService).Namespace
                && t.Name.StartsWith('I')
                && t.Name.EndsWith("Service")
            )
            .SelectMany(t => t.GetMethods());

    [Fact]
    public void ServiceMethods_HaveACrossAccountCase_ForEveryPublicServiceMethod()
    {
        var missing = CaseNames()
            .Select(r => r.Data)
            .Except(Cases().Keys, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Add a cross account case for: {string.Join(", ", missing)}"
        );
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task ServiceMethod_LeavesAnotherAccountUntouchedAndUnseen_ForTheCallersOwnAccountId(
        string caseName
    )
    {
        var before = await VictimSnapshotAsync();

        var result = await Cases()[caseName]();

        Assert.Equal(before, await VictimSnapshotAsync());
        var leaked = VictimMarkers()
            .Where(marker => Mentions(JsonSerializer.Serialize(result, Json), marker))
            .ToList();
        Assert.True(
            leaked.Count == 0,
            $"{caseName} returned the other account's {string.Join(", ", leaked)}"
        );
    }

    private IEnumerable<string> VictimMarkers() =>
        [
            Victim.ToString(),
            _victimGrantId.ToString(),
            _victimCapacityGrantId.ToString(),
            _victimRequestId.ToString(),
            _victimRegistrationId.ToString(),
            VICTIM_PROVIDER,
            VICTIM_GRANT_QUANTITY.ToString(),
            VICTIM_REQUEST_QUANTITY.ToString(),
            VICTIM_CONSUMED.ToString(),
        ];

    private static bool Mentions(string json, string marker) =>
        Regex.IsMatch(
            json,
            $"(?<![0-9A-Za-z-]){Regex.Escape(marker)}(?![0-9A-Za-z-])",
            RegexOptions.IgnoreCase
        );

    private Task<string> VictimSnapshotAsync() =>
        _host.QueryAsync(async db =>
            JsonSerializer.Serialize(
                new
                {
                    Grants = await db
                        .Grants.AsNoTracking()
                        .Where(g => g.AccountId == Victim)
                        .OrderBy(g => g.GrantId)
                        .ToListAsync(),
                    Requests = await db
                        .GrantRequests.AsNoTracking()
                        .Where(r => r.AccountId == Victim)
                        .OrderBy(r => r.GrantRequestId)
                        .ToListAsync(),
                    Events = await db
                        .ConsumptionEvents.AsNoTracking()
                        .Where(e => e.AccountId == Victim)
                        .OrderBy(e => e.ConsumptionId)
                        .ToListAsync(),
                    Registrations = await db
                        .Registrations.AsNoTracking()
                        .Where(r => r.AccountId == Victim)
                        .OrderBy(r => r.RegistrationId)
                        .ToListAsync(),
                },
                Json
            )
        );

    private async Task<Guid> CreateGrantAsync(
        Guid accountId,
        UsageOperation operation,
        GrantKind kind,
        long? quantity
    )
    {
        var result = await Grants<IGrantService, CreateGrantResult>(s =>
            s.CreateGrantAsync(
                new CreateGrantRequest(
                    accountId,
                    operation,
                    kind,
                    quantity,
                    _now.AddDays(-2),
                    _now.AddDays(30)
                )
            )
        );
        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        return result.CreatedId;
    }

    private Task<TResult> Grants<TService, TResult>(Func<TService, Task<TResult>> work)
        where TService : notnull =>
        _host.WithScopeAsync(services => work(services.GetRequiredService<TService>()));

    private async Task<object?> Call<TService>(Func<TService, Task> work)
        where TService : notnull =>
        await _host.WithScopeAsync<object?>(async services =>
        {
            var task = work(services.GetRequiredService<TService>());
            await task;
            return task.GetType().GetProperty("Result")?.GetValue(task);
        });

    private Task<object?> InOperationAsync<TResult>(
        string idempotencyScope,
        Func<IQuotaService, Task<TResult>> work
    ) =>
        _host.InOperationAsync<object?>(
            idempotencyScope,
            async services => await work(services.GetRequiredService<IQuotaService>())
        );
}
