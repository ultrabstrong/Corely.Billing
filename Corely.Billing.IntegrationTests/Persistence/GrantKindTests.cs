using Corely.Billing.Grants.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

public sealed class GrantKindTests : IDisposable
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly BillingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private DateTime Now => _host.TimeProvider.GetUtcNow().UtcDateTime;

    private async Task<Guid> GrantAsync(
        UsageOperation operation,
        GrantKind kind,
        long? quantity,
        int fromDays = -1,
        int toDays = 30
    )
    {
        var result = await _host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .CreateGrantAsync(
                    new CreateGrantRequest(
                        AccountId,
                        operation,
                        kind,
                        quantity,
                        Now.AddDays(fromDays),
                        Now.AddDays(toDays)
                    )
                )
        );
        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        return result.CreatedId;
    }

    private Task DisableAsync(Guid grantId) =>
        _host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .SetGrantDisabledAsync(new SetGrantDisabledRequest(AccountId, grantId, true))
        );

    private Task<T> QuotaAsync<T>(Func<IQuotaService, Task<T>> read) =>
        _host.WithScopeAsync(services => read(services.GetRequiredService<IQuotaService>()));

    private Task<RegisterResult> RegisterAsync(string? reference = null) =>
        QuotaAsync(q =>
            q.RegisterAsync(new RegisterRequest(AccountId, TestUsage.Members, reference))
        );

    [Fact]
    public async Task GetAllowanceAsync_AddsOnlyActiveAllowances_ForMixedGrants()
    {
        await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 60);
        await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 30);
        var disabled = await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 1_000);
        await DisableAsync(disabled);
        await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 500, fromDays: -60, toDays: -1);
        await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 700, fromDays: 5, toDays: 60);

        var allowance = await QuotaAsync(q => q.GetAllowanceAsync(AccountId, TestUsage.Retention));

        Assert.Equal(90, allowance.Total);
        Assert.Equal(2, allowance.Grants.Count);
    }

    [Fact]
    public async Task GetAllowanceAsync_IsUnlimited_ForOneUnlimitedAllowance()
    {
        await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 60);
        await GrantAsync(TestUsage.Retention, GrantKind.Allowance, null);

        var allowance = await QuotaAsync(q => q.GetAllowanceAsync(AccountId, TestUsage.Retention));

        Assert.Null(allowance.Total);
    }

    [Fact]
    public async Task HasFeatureAsync_FollowsTheGrant_ForAFeatureDisabledAndEnabled()
    {
        var feature = await GrantAsync(TestUsage.Sftp, GrantKind.Feature, null);

        var on = await QuotaAsync(q => q.HasFeatureAsync(AccountId, TestUsage.Sftp));
        await DisableAsync(feature);
        var off = await QuotaAsync(q => q.HasFeatureAsync(AccountId, TestUsage.Sftp));

        Assert.True(on.HasFeature);
        Assert.False(off.HasFeature);
    }

    [Fact]
    public async Task GetCapacityAsync_AddsCapacityAndCountsWhatIsRegistered_ForTwoGrants()
    {
        await GrantAsync(TestUsage.Members, GrantKind.Capacity, 2);
        await GrantAsync(TestUsage.Members, GrantKind.Capacity, 3, toDays: 90);
        await RegisterAsync("ada");
        await RegisterAsync("bob");

        var capacity = await QuotaAsync(q => q.GetCapacityAsync(AccountId, TestUsage.Members));

        Assert.Equal(5, capacity.Granted);
        Assert.Equal(2, capacity.InUse);
    }

    [Fact]
    public async Task RegisterAsync_TakesTheLongestLivedGrant_ForTwoWithRoom()
    {
        await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 7);
        var longLived = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 365);

        var result = await RegisterAsync("ada");

        Assert.Equal(RegisterResultCode.Registered, result.ResultCode);
        Assert.Equal(longLived, result.Registration!.GrantId);
    }

    [Fact]
    public async Task RegisterAsync_RefusesASecondRegistration_ForAReferenceAlreadyHeld()
    {
        await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5);
        await RegisterAsync("ada");

        var second = await RegisterAsync("ada");
        var capacity = await QuotaAsync(q => q.GetCapacityAsync(AccountId, TestUsage.Members));

        Assert.Equal(RegisterResultCode.AlreadyRegisteredError, second.ResultCode);
        Assert.Equal(1, capacity.InUse);
    }

    [Fact]
    public async Task RegisterAsync_RefusesAndReleasingFreesRoom_ForAFullGrant()
    {
        await GrantAsync(TestUsage.Members, GrantKind.Capacity, 1);
        var ada = await RegisterAsync("ada");

        var full = await RegisterAsync("bob");
        await QuotaAsync(q =>
            q.ReleaseRegistrationAsync(AccountId, ada.Registration!.RegistrationId)
        );
        var afterRelease = await RegisterAsync("bob");

        Assert.Equal(RegisterResultCode.NoCapacityError, full.ResultCode);
        Assert.Equal(RegisterResultCode.Registered, afterRelease.ResultCode);
    }

    [Fact]
    public async Task RegisterAsync_RefusesWithWrongKind_ForANonCapacityOperation()
    {
        var result = await QuotaAsync(q =>
            q.RegisterAsync(new RegisterRequest(AccountId, TestUsage.Retention, "ada"))
        );

        Assert.Equal(RegisterResultCode.WrongKindError, result.ResultCode);
    }

    [Fact]
    public async Task ListRegistrationsAsync_ReportsTheGrantsState_ForADisabledThenEndedGrant()
    {
        var grant = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 10);
        await RegisterAsync("ada");
        await DisableAsync(grant);

        var whileDisabled = await QuotaAsync(q =>
            q.ListRegistrationsAsync(new ListRegistrationsRequest([AccountId]))
        );
        _host.TimeProvider.Advance(TimeSpan.FromDays(11));
        var afterItEnded = await QuotaAsync(q =>
            q.ListRegistrationsAsync(new ListRegistrationsRequest([AccountId]))
        );

        Assert.Equal(
            RegistrationStatus.GrantDisabled,
            Assert.Single(whileDisabled.Data!.Items).Status
        );
        Assert.Equal(RegistrationStatus.GrantEnded, Assert.Single(afterItEnded.Data!.Items).Status);
    }

    [Fact]
    public async Task MoveRegistrationsAsync_ReleasesAndRecreatesOnTheTarget_ForEveryActiveRegistration()
    {
        var from = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 10);
        await RegisterAsync("ada");
        await RegisterAsync("bob");
        var to = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 5);

        var moved = await QuotaAsync(q =>
            q.MoveRegistrationsAsync(new MoveRegistrationsRequest(AccountId, from, to))
        );
        var all = await QuotaAsync(q =>
            q.ListRegistrationsAsync(
                new ListRegistrationsRequest([AccountId], IncludeReleased: true)
            )
        );

        Assert.Equal(2, moved.Moved.Count);
        Assert.Equal(
            2,
            all.Data!.Items.Count(r => r.GrantId == from && r.Status == RegistrationStatus.Released)
        );
        Assert.Equal(
            2,
            all.Data.Items.Count(r => r.GrantId == to && r.Status == RegistrationStatus.Active)
        );
    }

    [Fact]
    public async Task MoveRegistrationsAsync_MovesNothing_ForATargetWithoutRoom()
    {
        var from = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 10);
        await RegisterAsync("ada");
        await RegisterAsync("bob");
        var to = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 1, toDays: 5);

        var moved = await QuotaAsync(q =>
            q.MoveRegistrationsAsync(new MoveRegistrationsRequest(AccountId, from, to))
        );
        var capacity = await QuotaAsync(q => q.GetCapacityAsync(AccountId, TestUsage.Members));

        Assert.Equal(MoveRegistrationsResultCode.NoCapacityError, moved.ResultCode);
        Assert.Equal(2, capacity.Grants.Single(g => g.GrantId == from).InUse);
    }

    [Fact]
    public async Task DeleteGrantAsync_ReleasesItsRegistrations_ForACapacityGrant()
    {
        var doomed = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 10);
        await RegisterAsync("ada");
        await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 5);

        await _host.WithScopeAsync(services =>
            services.GetRequiredService<IGrantService>().DeleteGrantAsync(AccountId, doomed)
        );
        var active = await QuotaAsync(q =>
            q.ListRegistrationsAsync(new ListRegistrationsRequest([AccountId]))
        );
        var again = await RegisterAsync("ada");

        Assert.Empty(active.Data!.Items);
        Assert.Equal(RegisterResultCode.Registered, again.ResultCode);
    }

    [Fact]
    public async Task RegisterAsync_RegistersAgain_ForAReferenceHeldOnlyOnAnEndedGrant()
    {
        await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 10);
        await RegisterAsync("ada");
        _host.TimeProvider.Advance(TimeSpan.FromDays(11));
        var renewal = await GrantAsync(TestUsage.Members, GrantKind.Capacity, 5, toDays: 30);

        var before = await QuotaAsync(q => q.GetCapacityAsync(AccountId, TestUsage.Members));
        var again = await RegisterAsync("ada");

        Assert.Equal(0, before.InUse);
        Assert.Equal(RegisterResultCode.Registered, again.ResultCode);
        Assert.Equal(renewal, again.Registration!.GrantId);
    }

    [Fact]
    public async Task ListGrantsAsync_FiltersByKindOperationAndActiveDate_ForMixedGrants()
    {
        await GrantAsync(TestUsage.Extraction, GrantKind.Consumable, 100);
        var retention = await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 60);
        await GrantAsync(TestUsage.Retention, GrantKind.Allowance, 30, fromDays: 5, toDays: 60);

        var result = await _host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .ListGrantsAsync(
                    new ListGrantsRequest(
                        [AccountId],
                        Kind: GrantKind.Allowance,
                        Operation: TestUsage.Retention,
                        ActiveAtUtc: Now
                    )
                )
        );

        Assert.Equal(retention, Assert.Single(result.Data!.Items).GrantId);
    }
}
