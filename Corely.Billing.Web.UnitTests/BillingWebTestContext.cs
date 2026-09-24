using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.Web.UnitTests;

public abstract class BillingWebTestContext : BunitContext
{
    protected static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    protected static readonly UsageOperation Extraction = UsageOperation.From(
        "document_extraction"
    );
    protected static readonly UsageUnit Page = UsageUnit.From("page");

    protected Mock<IGrantService> Grants { get; } = new();
    protected Mock<IConsumptionService> Consumption { get; } = new();

    protected BillingWebTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<BillingCallGate>();
        Services.AddSingleton(Grants.Object);
        Services.AddSingleton(Consumption.Object);
        Services.AddSingleton<IUsageVocabulary>(new TestVocabulary());
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));

        HaveGrants();
        HaveUsed();
        Consumption
            .Setup(c =>
                c.ListConsumptionEventsAsync(
                    It.IsAny<ListConsumptionEventsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<ConsumptionEvent>(
                    RetrieveResultCode.Success,
                    string.Empty,
                    PagedResult<ConsumptionEvent>.Empty()
                )
            );
    }

    protected void HaveGrants(params Grant[] grants) =>
        Grants
            .Setup(g =>
                g.ListGrantsAsync(It.IsAny<ListGrantsRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new RetrieveListResult<Grant>(
                    RetrieveResultCode.Success,
                    string.Empty,
                    new PagedResult<Grant>([.. grants], grants.Length, 1, false)
                )
            );

    protected void HaveUsed(params GrantTotalConsumptions[] totals) =>
        Consumption
            .Setup(c =>
                c.GetGrantConsumptionTotalsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveSingleResult<List<GrantTotalConsumptions>>(
                    RetrieveResultCode.Success,
                    string.Empty,
                    [.. totals]
                )
            );

    protected static Grant Grant(long? quantity, int fromDays = -10, int toDays = 30) =>
        new()
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = Extraction,
            Unit = Page,
            Quantity = quantity,
            ValidFromUtc = Now.AddDays(fromDays),
            ValidToUtc = Now.AddDays(toDays),
        };

    private sealed class TestVocabulary : IUsageVocabulary
    {
        public IReadOnlyList<UsageOperationDefinition> Operations { get; } =
        [new(Extraction, "Document extraction")];

        public IReadOnlyList<UsageUnitDefinition> Units { get; } = [new(Page, "page")];

        public bool Knows(UsageOperation operation) => operation == Extraction;

        public bool Knows(UsageUnit unit) => unit == Page;

        public string DisplayName(UsageOperation operation) =>
            operation == Extraction ? "Document extraction" : operation.Value;

        public string DisplayName(UsageUnit unit) => unit == Page ? "page" : unit.Value;
    }
}
