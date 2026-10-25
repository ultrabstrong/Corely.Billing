using Corely.Billing.Grants.Models;
using Corely.Billing.Registrations.Processors;

namespace Corely.Billing.UnitTests.Registrations.Processors;

public class RegistrationSelectionPolicyTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly RegistrationSelectionPolicy _policy = new();

    private static Grant Grant(long? capacity, int endsInDays) =>
        new()
        {
            GrantId = Guid.CreateVersion7(),
            Kind = GrantKind.Capacity,
            Quantity = capacity,
            ValidFromUtc = Now.AddDays(-1),
            ValidToUtc = Now.AddDays(endsInDays),
        };

    [Fact]
    public void Pick_TakesTheLongestLivedGrant_ForTwoGrantsWithRoom()
    {
        var endingSoon = Grant(5, endsInDays: 7);
        var longLived = Grant(5, endsInDays: 365);

        Assert.Same(longLived, _policy.Pick([endingSoon, longLived], new Dictionary<Guid, long>()));
    }

    [Fact]
    public void Pick_SkipsAFullGrant_ForTheLongestLivedGrantBeingFull()
    {
        var endingSoon = Grant(5, endsInDays: 7);
        var longLived = Grant(2, endsInDays: 365);

        var picked = _policy.Pick(
            [endingSoon, longLived],
            new Dictionary<Guid, long> { [longLived.GrantId] = 2 }
        );

        Assert.Same(endingSoon, picked);
    }

    [Fact]
    public void Pick_ReturnsNothing_ForEveryGrantFull()
    {
        var full = Grant(1, endsInDays: 30);

        Assert.Null(_policy.Pick([full], new Dictionary<Guid, long> { [full.GrantId] = 1 }));
    }

    [Fact]
    public void Pick_TakesAnUnlimitedGrant_ForAnyNumberHeld()
    {
        var unlimited = Grant(null, endsInDays: 30);

        Assert.Same(
            unlimited,
            _policy.Pick([unlimited], new Dictionary<Guid, long> { [unlimited.GrantId] = 10_000 })
        );
    }
}
