using Corely.Billing.Usage;

namespace Corely.Billing.UnitTests.Usage;

public class UsageVocabularyTests
{
    private readonly IUsageVocabulary _vocabulary = TestUsage.Vocabulary;

    [Fact]
    public void Knows_IsTrue_ForARegisteredOperation() =>
        Assert.True(_vocabulary.Knows(TestUsage.Extraction));

    [Fact]
    public void Knows_IsFalse_ForAnUnregisteredOperation() =>
        Assert.False(_vocabulary.Knows(TestUsage.Unregistered));

    [Fact]
    public void Knows_IsFalse_ForTheDefaultOperation() =>
        Assert.False(_vocabulary.Knows(default(UsageOperation)));

    [Fact]
    public void Knows_IsTrue_ForARegisteredUnit() => Assert.True(_vocabulary.Knows(TestUsage.Page));

    [Fact]
    public void Knows_IsFalse_ForAnUnregisteredUnit() =>
        Assert.False(_vocabulary.Knows(TestUsage.UnregisteredUnit));

    [Fact]
    public void DisplayName_IsTheRegisteredName_ForARegisteredOperation() =>
        Assert.Equal("Extraction", _vocabulary.DisplayName(TestUsage.Extraction));

    [Fact]
    public void DisplayName_FallsBackToTheValue_ForAnUnregisteredOperation() =>
        Assert.Equal("retired", _vocabulary.DisplayName(UsageOperation.From("retired")));

    [Fact]
    public void DisplayName_FallsBackToTheValue_ForAnUnregisteredUnit() =>
        Assert.Equal("retired", _vocabulary.DisplayName(UsageUnit.From("retired")));
}
