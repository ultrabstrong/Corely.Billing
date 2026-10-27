using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Registrations.Enums;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;
using Corely.Billing.Web.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.UnitTests.Components;

public class RegistrationListTests : BillingWebTestContext
{
    private readonly Grant _grant = Capacity(5, inUse: 2);
    private readonly Grant _renewal = Capacity(5, inUse: 0);

    private Registration Held(
        string reference,
        RegistrationStatus status = RegistrationStatus.Active,
        Grant? on = null
    ) =>
        new(
            Guid.CreateVersion7(),
            AccountId,
            (on ?? _grant).GrantId,
            Members,
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

    private IRenderedComponent<RegistrationList> Render(Guid? grantId = null) =>
        Render<RegistrationList>(p =>
            p.Add(c => c.AccountId, AccountId)
                .Add(c => c.Operation, Members)
                .Add(c => c.GrantId, grantId)
        );

    [Fact]
    public void Render_ListsEachRegistrationWithItsStatus_ForAGrantsRegistrations()
    {
        HaveGrants(_grant);
        HaveRegistrations(Held("ada"), Held("bob", RegistrationStatus.GrantDisabled));

        var items = Render(_grant.GrantId).FindAll("[data-registration-id]");

        Assert.Equal(2, items.Count);
        Assert.Contains("ada", items[0].TextContent);
        Assert.Contains("Active", items[0].TextContent);
        Assert.Contains("Grant disabled", items[1].TextContent);
        Assert.Empty(items[0].QuerySelectorAll("[data-registration-grant]"));
    }

    [Fact]
    public void Render_NamesEachRegistrationsGrant_ForAWholeOperation()
    {
        HaveGrants(_grant, _renewal);
        HaveRegistrations(Held("ada"), Held("bob", on: _renewal));

        var grants = Render().FindAll("[data-registration-grant]");

        Assert.Equal(2, grants.Count);
        Assert.Contains(
            _grant.HeadlineAndWindow(Services.GetRequiredService<IUsageVocabulary>()),
            grants[0].TextContent
        );
    }

    [Fact]
    public void Release_ReleasesThatRegistration_ForItsButton()
    {
        var ada = Held("ada");
        HaveGrants(_grant);
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

        Render(_grant.GrantId).Find("[data-registration-id] button.btn-outline-danger").Click();

        Quota.Verify(q =>
            q.ReleaseRegistrationAsync(AccountId, ada.RegistrationId, It.IsAny<CancellationToken>())
        );
    }

    [Fact]
    public void Move_MovesThatRegistrationAlone_ForItsRowsButton()
    {
        var ada = Held("ada");
        HaveGrants(_grant, _renewal);
        HaveRegistrations(ada, Held("bob"));
        Quota
            .Setup(q =>
                q.MoveRegistrationsAsync(
                    It.IsAny<MoveRegistrationsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new MoveRegistrationsResult(MoveRegistrationsResultCode.Success, "", []));

        Render().Find("[data-registration-id] button.btn-outline-primary").Click();

        Quota.Verify(q =>
            q.MoveRegistrationsAsync(
                It.Is<MoveRegistrationsRequest>(r =>
                    r.FromGrantId == _grant.GrantId
                    && r.ToGrantId == _renewal.GrantId
                    && r.RegistrationIds!.SequenceEqual(new[] { ada.RegistrationId })
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Fact]
    public void Render_LoadsOnce_ForTheSameParametersSentAgain()
    {
        HaveGrants(_grant);
        HaveRegistrations(Held("ada"));
        var list = Render();

        list.Render(p =>
            p.Add(c => c.AccountId, AccountId)
                .Add(c => c.Operation, Members)
                .Add(c => c.GrantId, null)
        );

        Quota.Verify(
            q =>
                q.ListRegistrationsAsync(
                    It.IsAny<ListRegistrationsRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Render_SaysNothingIsRegistered_ForNoRegistrations()
    {
        HaveGrants(_grant);
        HaveRegistrations();

        Assert.Contains("Nothing is registered on this grant", Render(_grant.GrantId).Markup);
    }
}
