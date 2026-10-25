using Corely.Billing.Grants.Constants;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;

namespace Corely.Billing.Demos.Subscription;

public sealed class Membership(IGrantService grants, IQuotaService quota, TimeProvider timeProvider)
{
    public const int CAPACITY = 3;
    public const string YOU = "you";

    public static readonly Guid AccountId = Guid.Parse("0199a0de-0000-7000-8000-00000000c0de");
    public static readonly UsageOperation Access = UsageOperation.From("members_area");
    public static readonly UsageUnit Member = UsageUnit.From("member");

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Grant?> SubscriptionAsync()
    {
        var result = await grants.ListGrantsAsync(
            new ListGrantsRequest([AccountId], Kind: GrantKind.Capacity, Operation: Access, Take: 1)
        );
        return result.Data?.Items.FirstOrDefault();
    }

    public async Task SubscribeAsync()
    {
        if (await SubscriptionAsync() is { } lapsed)
        {
            await grants.SetGrantDisabledAsync(
                new SetGrantDisabledRequest(AccountId, lapsed.GrantId, Disabled: false)
            );
            return;
        }

        await grants.CreateGrantAsync(
            new CreateGrantRequest(
                AccountId,
                Access,
                GrantKind.Capacity,
                CAPACITY,
                Now,
                GrantConstants.NEVER_EXPIRES_UTC
            )
        );
    }

    public async Task CancelAsync()
    {
        if (await SubscriptionAsync() is { } subscription)
        {
            await grants.SetGrantDisabledAsync(
                new SetGrantDisabledRequest(AccountId, subscription.GrantId, Disabled: true)
            );
        }
    }

    public Task<GetCapacityResult> CapacityAsync() => quota.GetCapacityAsync(AccountId, Access);

    public Task<RegisterResult> JoinAsync(string reference) =>
        quota.RegisterAsync(new RegisterRequest(AccountId, Access, reference));
}
