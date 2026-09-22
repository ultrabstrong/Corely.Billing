using Corely.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.UnitTests;

public class UsageVocabularyTests
{
    private static IUsageVocabulary Build(Action<UsageVocabularyBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddUsageVocabulary(configure);
        return services.BuildServiceProvider().GetRequiredService<IUsageVocabulary>();
    }

    private static IUsageVocabulary BuildDefault() =>
        Build(vocabulary =>
            vocabulary.Operation("document_extraction", "Document Extraction").Unit("page", "page")
        );

    [Fact]
    public void Knows_IsTrue_ForARegisteredOperation() =>
        Assert.True(BuildDefault().Knows(UsageOperation.From("document_extraction")));

    [Fact]
    public void Knows_IsFalse_ForAnUnregisteredOperation() =>
        Assert.False(BuildDefault().Knows(UsageOperation.From("invented")));

    [Fact]
    public void Knows_IsFalse_ForTheDefaultOperation() =>
        Assert.False(BuildDefault().Knows(default(UsageOperation)));

    [Fact]
    public void Knows_IsTrue_ForARegisteredUnit() =>
        Assert.True(BuildDefault().Knows(UsageUnit.From("page")));

    [Fact]
    public void Knows_IsFalse_ForAnUnregisteredUnit() =>
        Assert.False(BuildDefault().Knows(UsageUnit.From("byte")));

    [Fact]
    public void DisplayName_IsTheRegisteredName_ForARegisteredOperation() =>
        Assert.Equal(
            "Document Extraction",
            BuildDefault().DisplayName(UsageOperation.From("document_extraction"))
        );

    [Fact]
    public void DisplayName_FallsBackToTheValue_ForAnUnregisteredOperation() =>
        Assert.Equal("retired", BuildDefault().DisplayName(UsageOperation.From("retired")));

    [Fact]
    public void Operations_KeepRegistrationOrder_ForSeveralOperations()
    {
        var vocabulary = Build(v =>
            v.Operation("second", "Second").Operation("first", "First").Unit("page", "page")
        );

        Assert.Equal(
            ["second", "first"],
            vocabulary.Operations.Select(definition => definition.Operation.Value)
        );
    }

    [Fact]
    public void AddUsageVocabulary_Throws_ForADuplicateOperation() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            Build(v => v.Operation("a", "A").Operation("a", "A again").Unit("page", "page"))
        );

    [Fact]
    public void AddUsageVocabulary_Throws_ForADuplicateUnit() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            Build(v => v.Operation("a", "A").Unit("page", "page").Unit("page", "page again"))
        );

    [Fact]
    public void AddUsageVocabulary_Throws_ForAVocabularyWithNoUnits() =>
        Assert.Throws<InvalidOperationException>(() => Build(v => v.Operation("a", "A")));

    [Fact]
    public void AddUsageVocabulary_Throws_ForAVocabularyWithNoOperations() =>
        Assert.Throws<InvalidOperationException>(() => Build(v => v.Unit("page", "page")));
}
