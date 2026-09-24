using Corely.Billing.Grants.Models;
using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Common.Filtering;
using Corely.Common.Filtering.Filters;
using Corely.Common.Filtering.Ordering;

namespace Corely.Billing.Demos.Subscription;

public sealed class Membership(
    IGrantService grants,
    IQuotaService quota,
    IConsumptionService consumption,
    IOperationContextAccessor operations,
    TimeProvider timeProvider
)
{
    public static readonly Guid AccountId = Guid.Parse("0199a0de-0000-7000-8000-00000000c0de");
    public static readonly UsageOperation Access = UsageOperation.From("members_area");
    public static readonly UsageUnit Visit = UsageUnit.From("visit");

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Grant?> CurrentTermAsync()
    {
        var now = Now;
        var result = await grants.ListGrantsAsync(
            new ListGrantsRequest(
                AccountId,
                Filter: Filter
                    .For<Grant>()
                    .Where(g => g.ValidFromUtc, ComparableFilter<DateTime>.LessThanOrEqual(now))
                    .Where(g => g.ValidToUtc, ComparableFilter<DateTime>.GreaterThan(now)),
                Order: Order.For<Grant>().By(g => g.ValidToUtc, SortDirection.Descending),
                Take: 1
            )
        );
        return result.Data?.Items.FirstOrDefault();
    }

    public Task SubscribeAsync() =>
        grants.CreateGrantAsync(
            new CreateGrantRequest(AccountId, Access, Visit, Quantity: null, Now, Now.AddYears(1))
        );

    public async Task CancelAsync()
    {
        if (await CurrentTermAsync() is not { } term)
            return;

        await grants.UpdateGrantAsync(
            new UpdateGrantRequest(
                AccountId,
                term.GrantId,
                Visit,
                term.Quantity,
                term.ValidFromUtc,
                Now
            )
        );
    }

    public async Task<bool> IsEntitledAsync() =>
        await quota.GetAvailabilityAsync(AccountId, Access, Visit) == QuotaAvailability.Available;

    public async Task RecordVisitAsync()
    {
        using var scope = operations.BeginScope(
            new OperationContext(Guid.CreateVersion7(), $"visit:{Guid.CreateVersion7()}")
        );
        await quota.ReserveAsync(new ReserveQuotaRequest(AccountId, Access, Visit, 1, "web"));
        await quota.SettleAsync(new SettleQuotaRequest(AccountId, Access, Visit, 1));
    }

    public async Task<long> VisitsThisTermAsync(Grant term)
    {
        var totals = await consumption.GetGrantConsumptionTotalsAsync(AccountId, [term.GrantId]);
        return totals.Item?.FirstOrDefault()?.TotalConsumedQuantity ?? 0;
    }
}
