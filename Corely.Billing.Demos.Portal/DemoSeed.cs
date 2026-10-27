using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;

namespace Corely.Billing.Demos.Portal;

internal static class DemoSeed
{
    private const int HISTORY_DAYS = 180;

    public static async Task RunAsync(IServiceProvider services, SeedClock clock)
    {
        var today = clock.RealNow.UtcDateTime.Date;

        using (var scope = services.CreateScope())
        {
            var grantService = scope.ServiceProvider.GetRequiredService<IGrantService>();
            var existing = await grantService.ListGrantsAsync(
                new ListGrantsRequest([DemoUsage.AccountId], Take: 1)
            );
            if (existing.Data?.TotalCount > 0)
            {
                Console.WriteLine("The demo account already has grants. Nothing seeded.");
                return;
            }

            (UsageOperation, GrantKind, long?, int, int)[] grants =
            [
                (DemoUsage.TextGeneration, GrantKind.Consumable, 1_500_000, -HISTORY_DAYS, -90),
                (DemoUsage.TextGeneration, GrantKind.Consumable, 6_000_000, -90, 275),
                (DemoUsage.TextGeneration, GrantKind.Consumable, 500_000, -30, 10),
                (DemoUsage.TextGeneration, GrantKind.Consumable, 6_000_000, 20, 385),
                (DemoUsage.Embeddings, GrantKind.Consumable, null, -120, 245),
                (DemoUsage.ImageGeneration, GrantKind.Consumable, 1_000, -HISTORY_DAYS, 185),
                (DemoUsage.TeamMembers, GrantKind.Capacity, 5, -60, 305),
                (DemoUsage.LogRetention, GrantKind.Allowance, 60, -30, 335),
                (DemoUsage.FineTuning, GrantKind.Feature, null, -10, 355),
            ];
            foreach (var (operation, kind, quantity, fromDays, toDays) in grants)
            {
                await grantService.CreateGrantAsync(
                    new CreateGrantRequest(
                        DemoUsage.AccountId,
                        operation,
                        kind,
                        quantity,
                        today.AddDays(fromDays),
                        today.AddDays(toDays)
                    )
                );
            }

            var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();
            foreach (
                var (reference, description) in new[]
                {
                    ("ada", "Ada Lovelace (owner)"),
                    ("grace", "Grace Hopper"),
                    ("alan", "Alan Turing"),
                }
            )
            {
                await quotaService.RegisterAsync(
                    new RegisterRequest(
                        DemoUsage.AccountId,
                        DemoUsage.TeamMembers,
                        reference,
                        description
                    )
                );
            }

            await scope
                .ServiceProvider.GetRequiredService<IGrantRequestService>()
                .RequestGrantAsync(
                    new RequestGrantRequest(
                        DemoUsage.AccountId,
                        DemoUsage.ImageGeneration,
                        2_000,
                        "Launching product photos next month"
                    )
                );
        }

        var random = new Random(42);
        var requests = 0;
        for (var day = -HISTORY_DAYS; day < 0; day++)
        {
            foreach (var (count, run) in DailyWork(random))
            {
                for (var i = 0; i < count; i++)
                {
                    clock.Now = new DateTimeOffset(
                        today.AddDays(day).AddHours(8 + random.Next(10)).AddMinutes(random.Next(60))
                    );
                    using var scope = services.CreateScope();
                    await run(scope.ServiceProvider.GetRequiredService<UsageSimulator>());
                    requests++;
                }
            }
        }

        Console.WriteLine(
            $"Seeded 9 grants, 3 team members, a pending grant request and {requests:N0} model requests over {HISTORY_DAYS} days."
        );
    }

    private static (int Count, Func<UsageSimulator, Task<string>> Run)[] DailyWork(Random random) =>
        [
            (
                random.Next(0, 4),
                s =>
                    s.GenerateTextAsync(
                        random.Next(1, 30_000),
                        random.Next(3) == 0 ? "large-model" : "fast-model"
                    )
            ),
            (random.Next(0, 3), s => s.EmbedAsync(random.Next(1, 10_000))),
            (random.Next(0, 2), s => s.GenerateImagesAsync(random.Next(1, 8))),
        ];
}
