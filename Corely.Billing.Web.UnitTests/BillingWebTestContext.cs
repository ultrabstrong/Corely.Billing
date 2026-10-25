using Corely.Billing.Consumption.Models;
using Corely.Billing.GrantRequests.Models;
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
    protected static readonly UsageOperation Retention = UsageOperation.From("document_retention");
    protected static readonly UsageOperation Sftp = UsageOperation.From("sftp_access");
    protected static readonly UsageOperation Seats = UsageOperation.From("seats");
    protected static readonly UsageUnit Day = UsageUnit.From("day");
    protected static readonly UsageUnit User = UsageUnit.From("user");

    protected Mock<IGrantService> Grants { get; } = new();
    protected Mock<IGrantRequestService> GrantRequests { get; } = new();
    protected Mock<IConsumptionService> Consumption { get; } = new();
    protected Mock<IQuotaService> Quota { get; } = new();
    protected RecordingGrantActionGate ActionGate { get; } = new();

    protected BillingWebTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddOptions();
        Services.AddScoped<UsageExporter>();
        Services.AddSingleton(
            Mock.Of<IBillingAccountAccessor>(a =>
                a.GetAccountIdAsync() == Task.FromResult<Guid?>(AccountId)
            )
        );
        Services.AddSingleton<IGrantActionGate>(ActionGate);
        Services.AddSingleton(Grants.Object);
        Services.AddSingleton(GrantRequests.Object);
        Services.AddSingleton(Consumption.Object);
        Services.AddSingleton(Quota.Object);
        Services.AddSingleton<IUsageVocabulary>(new TestVocabulary());
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));

        HaveGrants();
        HaveRequests();
        HaveSeries();
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
                g.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<Grant>(
                    RetrieveResultCode.Success,
                    string.Empty,
                    new PagedResult<Grant>([.. grants], grants.Length, 1, false)
                )
            );

    protected void DenyGrants() =>
        Grants
            .Setup(g =>
                g.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<Grant>(RetrieveResultCode.UnauthorizedError, "no", null)
            );

    protected void HaveRequests(params GrantRequest[] requests) =>
        GrantRequests
            .Setup(g =>
                g.ListGrantRequestsAsync(
                    It.IsAny<ListGrantRequestsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<GrantRequest>(
                    RetrieveResultCode.Success,
                    string.Empty,
                    new PagedResult<GrantRequest>([.. requests], requests.Length, 1, false)
                )
            );

    protected void DenyRequests() =>
        GrantRequests
            .Setup(g =>
                g.ListGrantRequestsAsync(
                    It.IsAny<ListGrantRequestsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<GrantRequest>(
                    RetrieveResultCode.UnauthorizedError,
                    "no",
                    null
                )
            );

    protected static GrantRequest Request(long quantity, string? note = null, int daysAgo = 2) =>
        new()
        {
            GrantRequestId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = Extraction,
            Kind = GrantKind.Consumable,
            Unit = Page,
            Quantity = quantity,
            Note = note,
            RequestedUtc = Now.AddDays(-daysAgo),
            Status = GrantRequestStatus.Pending,
        };

    protected void HaveSeries(params ConsumptionSeries[] series) =>
        Consumption
            .Setup(c =>
                c.GetConsumptionTimeSeriesAsync(
                    It.IsAny<GetConsumptionTimeSeriesRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveSingleResult<List<ConsumptionSeries>>(
                    RetrieveResultCode.Success,
                    string.Empty,
                    [.. series]
                )
            );

    protected static Grant Grant(long? quantity, int fromDays = -10, int toDays = 30, long used = 0)
    {
        var grant = new Grant
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = Extraction,
            Kind = GrantKind.Consumable,
            Unit = Page,
            Quantity = quantity,
            ValidFromUtc = Now.AddDays(fromDays),
            ValidToUtc = Now.AddDays(toDays),
        };
        grant.WithUse(used);
        return grant;
    }

    protected static Grant Retained(long? days, int toDays = 30) =>
        new()
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = Retention,
            Kind = GrantKind.Allowance,
            Unit = Day,
            Quantity = days,
            ValidFromUtc = Now.AddDays(-10),
            ValidToUtc = Now.AddDays(toDays),
        };

    protected static Grant Feature(bool disabled = false) =>
        new()
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = Sftp,
            Kind = GrantKind.Feature,
            ValidFromUtc = Now.AddDays(-10),
            ValidToUtc = Now.AddDays(30),
            Disabled = disabled,
        };

    protected static Grant Capacity(long? seats, long inUse) =>
        new()
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = Seats,
            Kind = GrantKind.Capacity,
            Unit = User,
            Quantity = seats,
            InUse = inUse,
            ValidFromUtc = Now.AddDays(-10),
            ValidToUtc = Now.AddDays(30),
        };

    private sealed class TestVocabulary : IUsageVocabulary
    {
        public IReadOnlyList<UsageOperationDefinition> Operations { get; } =
        [
            new(Extraction, "Document extraction", GrantKind.Consumable, Page),
            new(Seats, "Seats", GrantKind.Capacity, User),
            new(Retention, "Document retention", GrantKind.Allowance, Day),
            new(Sftp, "SFTP access", GrantKind.Feature, null),
        ];

        public IReadOnlyList<UsageUnitDefinition> Units { get; } =
        [new(Page, "page"), new(Day, "day"), new(User, "user")];

        public bool Knows(UsageOperation operation) => Definition(operation) is not null;

        public bool Knows(UsageUnit unit) => Units.Any(u => u.Unit == unit);

        public UsageOperationDefinition? Definition(UsageOperation operation) =>
            Operations.FirstOrDefault(o => o.Operation == operation);

        public string DisplayName(UsageOperation operation) =>
            Definition(operation)?.DisplayName ?? operation.Value;

        public string DisplayName(UsageUnit unit) =>
            Units.FirstOrDefault(u => u.Unit == unit)?.DisplayName ?? unit.Value;
    }
}
