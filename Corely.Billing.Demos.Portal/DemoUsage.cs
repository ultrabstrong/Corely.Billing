using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Demos.Portal;

internal static class DemoUsage
{
    public static readonly Guid AccountId = Guid.Parse("0199a0de-0000-7000-8000-00000000d3e0");

    public static readonly UsageOperation TextGeneration = UsageOperation.From("text_generation");
    public static readonly UsageOperation Embeddings = UsageOperation.From("embeddings");
    public static readonly UsageOperation ImageGeneration = UsageOperation.From("image_generation");
    public static readonly UsageOperation TeamMembers = UsageOperation.From("team_members");
    public static readonly UsageOperation LogRetention = UsageOperation.From("log_retention");
    public static readonly UsageOperation FineTuning = UsageOperation.From("fine_tuning");
    public static readonly UsageUnit Token = UsageUnit.From("token");
    public static readonly UsageUnit Image = UsageUnit.From("image");
    public static readonly UsageUnit Member = UsageUnit.From("member");
    public static readonly UsageUnit Day = UsageUnit.From("day");

    public static BillingOptions Register(BillingOptions options) =>
        options
            .RegisterUnit(Token.Value, "token")
            .RegisterUnit(Image.Value, "image")
            .RegisterUnit(Member.Value, "member")
            .RegisterUnit(Day.Value, "day")
            .RegisterOperation(
                TextGeneration.Value,
                "Text generation",
                GrantKind.Consumable,
                Token.Value
            )
            .RegisterOperation(Embeddings.Value, "Embeddings", GrantKind.Consumable, Token.Value)
            .RegisterOperation(
                ImageGeneration.Value,
                "Image generation",
                GrantKind.Consumable,
                Image.Value
            )
            .RegisterOperation(TeamMembers.Value, "Team members", GrantKind.Capacity, Member.Value)
            .RegisterOperation(LogRetention.Value, "Log retention", GrantKind.Allowance, Day.Value)
            .RegisterOperation(FineTuning.Value, "Fine tuning", GrantKind.Feature);
}
