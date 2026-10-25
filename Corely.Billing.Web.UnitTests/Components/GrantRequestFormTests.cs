using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class GrantRequestFormTests : BillingWebTestContext
{
    private RequestGrantRequest? _sent;
    private RequestGrantResultCode _code = RequestGrantResultCode.Success;
    private Guid? _raised;

    public GrantRequestFormTests() =>
        GrantRequests
            .Setup(g =>
                g.RequestGrantAsync(It.IsAny<RequestGrantRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<RequestGrantRequest, CancellationToken>((r, _) => _sent = r)
            .ReturnsAsync(() => new RequestGrantResult(_code, "rejected", Guid.CreateVersion7()));

    private IRenderedComponent<GrantRequestForm> Render() =>
        Render<GrantRequestForm>(p =>
            p.Add(c => c.AccountId, AccountId).Add(c => c.OnSent, (Guid id) => _raised = id)
        );

    [Fact]
    public void Send_RequestsWhatWasEntered_ForAFilledForm()
    {
        var form = Render();
        form.Find("#cbw-request-quantity").Change("2500");
        form.Find("#cbw-request-note").Change("Month end batch");

        form.Find("form").Submit();

        Assert.Equal(
            new RequestGrantRequest(AccountId, Extraction, 2_500, "Month end batch"),
            _sent
        );
        Assert.NotNull(_raised);
    }

    [Fact]
    public void Send_RefusesWithoutCallingTheService_ForZero()
    {
        var form = Render();

        form.Find("form").Submit();

        Assert.Equal("Ask for at least one.", form.Find(".alert-danger").TextContent);
        Assert.Null(_sent);
    }

    [Fact]
    public void Send_SaysOneIsPending_ForARequestAlreadyPending()
    {
        _code = RequestGrantResultCode.AlreadyPendingError;
        var form = Render();
        form.Find("#cbw-request-quantity").Change("10");

        form.Find("form").Submit();

        Assert.Equal(
            "This account already has a pending request for that.",
            form.Find(".alert-danger").TextContent
        );
        Assert.Null(_raised);
    }

    [Fact]
    public void Render_ShowsNoForm_ForACallerNotAllowedToRequest()
    {
        ActionGate.Deny(GrantAction.Request);

        var form = Render();

        Assert.Empty(form.FindAll("form"));
        Assert.Contains("You are not allowed to request grants.", form.Markup);
    }

    [Fact]
    public void Send_RequestsAFeatureWithNoQuantity_ForAFeatureOperation()
    {
        var form = Render();
        form.Find("#cbw-request-operation").Change(Sftp.Value);

        Assert.Empty(form.FindAll("#cbw-request-quantity"));
        form.Find("form").Submit();

        Assert.Equal(new RequestGrantRequest(AccountId, Sftp, null), _sent);
    }

    [Fact]
    public void Render_OffersOnlyDeclaredOperationsAndNoUnit_ForTheForm()
    {
        var form = Render();

        Assert.Empty(form.FindAll("#cbw-request-unit"));
        Assert.Equal(4, form.FindAll("#cbw-request-operation option").Count);
        Assert.Equal("pages", form.Find("[data-unit]").TextContent);
    }
}
