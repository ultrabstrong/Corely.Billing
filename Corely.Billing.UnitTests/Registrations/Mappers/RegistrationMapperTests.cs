using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Registrations.Entities;
using Corely.Billing.Registrations.Enums;
using Corely.Billing.Registrations.Mappers;
using Corely.Billing.Registrations.Models;

namespace Corely.Billing.UnitTests.Registrations.Mappers;

public class RegistrationMapperTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RegistrationEntity Registration(DateTime? releasedUtc = null) =>
        new()
        {
            RegistrationId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            GrantId = Guid.CreateVersion7(),
            Operation = TestUsage.Members,
            Reference = "user:ada",
            RegisteredUtc = Now.AddDays(-3),
            ReleasedUtc = releasedUtc,
        };

    private static Grant Grant(bool disabled = false, int endsInDays = 30) =>
        new()
        {
            Kind = GrantKind.Capacity,
            Disabled = disabled,
            ValidFromUtc = Now.AddDays(-10),
            ValidToUtc = Now.AddDays(endsInDays),
        };

    [Fact]
    public void StatusOn_IsActive_ForAHeldRegistrationOnALiveGrant() =>
        Assert.Equal(RegistrationStatus.Active, Registration().StatusOn(Grant(), Now));

    [Fact]
    public void StatusOn_IsReleased_ForAReleasedRegistrationWhateverItsGrant() =>
        Assert.Equal(
            RegistrationStatus.Released,
            Registration(releasedUtc: Now.AddDays(-1)).StatusOn(Grant(disabled: true), Now)
        );

    [Fact]
    public void StatusOn_IsGrantDisabled_ForAHeldRegistrationOnADisabledGrant() =>
        Assert.Equal(
            RegistrationStatus.GrantDisabled,
            Registration().StatusOn(Grant(disabled: true), Now)
        );

    [Fact]
    public void StatusOn_IsGrantEnded_ForAHeldRegistrationOnAnEndedGrant() =>
        Assert.Equal(
            RegistrationStatus.GrantEnded,
            Registration().StatusOn(Grant(endsInDays: -1), Now)
        );

    [Fact]
    public void MovedTo_KeepsTheReferenceAndDescriptionOnANewRow_ForATargetGrant()
    {
        var original = Registration();
        original.Description = "Ada (admin)";
        var target = Guid.CreateVersion7();

        var moved = original.MovedTo(target, Now);

        Assert.NotEqual(original.RegistrationId, moved.RegistrationId);
        Assert.Equal(target, moved.GrantId);
        Assert.Equal("user:ada", moved.Reference);
        Assert.Equal("Ada (admin)", moved.Description);
        Assert.Equal(Now, moved.RegisteredUtc);
        Assert.Null(moved.ReleasedUtc);
    }

    [Fact]
    public void ToEntity_TrimsAndBlanksTheText_ForARegisterRequest()
    {
        var entity = new RegisterRequest(
            Guid.CreateVersion7(),
            TestUsage.Members,
            "  bob ",
            "   "
        ).ToEntity(Guid.CreateVersion7(), Now);

        Assert.Equal("bob", entity.Reference);
        Assert.Null(entity.Description);
    }
}
