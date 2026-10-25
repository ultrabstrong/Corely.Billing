using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class RegistrationListTests : BillingWebTestContext
{
    private readonly Grant _grant = Capacity(5, inUse: 2);

    private Registration Held(
        string reference,
        RegistrationStatus status = RegistrationStatus.Active
    ) =>
        new(
            Guid.CreateVersion7(),
            AccountId,
            _grant.GrantId,
            Seats,
            reference,
            null,
            Now.AddDays(-3),
            null,
            status
        );

    private void HaveRegistrations(params Registration[] registrations) =>
        Quota
            .Setup(q =>
                q.ListRegistrationsAsync(
                    It.IsAny<ListRegistrationsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<Registration>(
                    RetrieveResultCode.Success,
                    "",
                    new PagedResult<Registration>(
                        [.. registrations],
                        registrations.Length,
                        1,
                        false
                    )
                )
            );

    private IRenderedComponent<RegistrationList> Render() =>
        Render<RegistrationList>(p => p.Add(c => c.AccountId, AccountId).Add(c => c.Grant, _grant));

    [Fact]
    public void Render_ListsEachSeatWithItsStatus_ForAGrantsRegistrations()
    {
        HaveRegistrations(Held("ada"), Held("bob", RegistrationStatus.GrantDisabled));

        var items = Render().FindAll("[data-registration-id]");

        Assert.Equal(2, items.Count);
        Assert.Contains("ada", items[0].TextContent);
        Assert.Contains("Active", items[0].TextContent);
        Assert.Contains("Grant disabled", items[1].TextContent);
    }

    [Fact]
    public void Release_ReleasesThatRegistration_ForItsButton()
    {
        var ada = Held("ada");
        HaveRegistrations(ada);
        Quota
            .Setup(q =>
                q.ReleaseRegistrationAsync(
                    AccountId,
                    ada.RegistrationId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ModifyResult(ModifyResultCode.Success, ""));

        Render().Find("[data-registration-id] button").Click();

        Quota.Verify(q =>
            q.ReleaseRegistrationAsync(AccountId, ada.RegistrationId, It.IsAny<CancellationToken>())
        );
    }

    [Fact]
    public void Render_SaysNoOneHoldsASeat_ForNoRegistrations()
    {
        HaveRegistrations();

        Assert.Contains("Nothing holds a seat", Render().Markup);
    }
}
