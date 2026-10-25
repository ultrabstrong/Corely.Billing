using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class UsageVocabularyExtensionsTests
{
    private static readonly UsageOperation Extraction = UsageOperation.From("extraction");
    private static readonly UsageOperation Images = UsageOperation.From("images");
    private static readonly UsageOperation Sftp = UsageOperation.From("sftp");
    private static readonly UsageUnit Page = UsageUnit.From("page");

    private static readonly IUsageVocabulary Vocabulary = Mock.Of<IUsageVocabulary>(v =>
        v.Operations
            == new List<UsageOperationDefinition>
            {
                new(Extraction, "Extraction", GrantKind.Consumable, Page),
                new(Sftp, "SFTP", GrantKind.Feature, null),
                new(Images, "Images", GrantKind.Consumable, Page),
            }
        && v.Units == new List<UsageUnitDefinition> { new(Page, "page") }
    );

    private static GrantRequest Pending(UsageOperation operation) =>
        new() { Operation = operation };

    [Fact]
    public void Unrequested_ListsEveryOperation_ForNoPendingRequests() =>
        Assert.Equal(
            [Extraction, Sftp, Images],
            Vocabulary.Unrequested([]).Select(o => o.Operation)
        );

    [Fact]
    public void Unrequested_LeavesOutAPendingOperation_ForOnePendingRequest() =>
        Assert.Equal(
            [Sftp, Images],
            Vocabulary.Unrequested([Pending(Extraction)]).Select(o => o.Operation)
        );

    [Fact]
    public void ByKind_GroupsOperationsInKindOrder_ForMixedKinds() =>
        Assert.Equal(
            [(GrantKind.Consumable, 2), (GrantKind.Feature, 1)],
            Vocabulary.ByKind().Select(g => (g.Key, g.Count()))
        );
}
