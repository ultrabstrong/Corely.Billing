using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class UsageVocabularyExtensionsTests
{
    private static readonly UsageOperation Extraction = UsageOperation.From("extraction");
    private static readonly UsageOperation Images = UsageOperation.From("images");
    private static readonly UsageUnit Page = UsageUnit.From("page");

    private static readonly IUsageVocabulary Vocabulary = Mock.Of<IUsageVocabulary>(v =>
        v.Operations
            == new List<UsageOperationDefinition>
            {
                new(Extraction, "Extraction"),
                new(Images, "Images"),
            }
        && v.Units == new List<UsageUnitDefinition> { new(Page, "page") }
    );

    private static GrantRequest Pending(UsageOperation operation) =>
        new() { Operation = operation, Unit = Page };

    [Fact]
    public void Unrequested_ListsEveryPair_ForNoPendingRequests() =>
        Assert.Equal([(Extraction, Page), (Images, Page)], Vocabulary.Unrequested([]));

    [Fact]
    public void Unrequested_LeavesOutAPendingPair_ForOnePendingRequest() =>
        Assert.Equal([(Images, Page)], Vocabulary.Unrequested([Pending(Extraction)]));

    [Fact]
    public void Unrequested_IsEmpty_ForEveryPairPending() =>
        Assert.Empty(Vocabulary.Unrequested([Pending(Extraction), Pending(Images)]));
}
