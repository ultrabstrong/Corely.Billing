using Corely.Billing.Usage;

namespace Corely.Billing.IAM.UnitTests;

internal static class TestUsage
{
    public static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid GrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly UsageOperation Extraction = UsageOperation.From("extraction");
    public static readonly UsageUnit Page = UsageUnit.From("page");
}
