using Corely.Billing.Grants.Models;
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
                new ListGrantsRequest(DemoUsage.AccountId, Take: 1)
            );
            if (existing.Data?.TotalCount > 0)
            {
                Console.WriteLine("The demo account already has grants. Nothing seeded.");
                return;
            }

            (UsageOperation, UsageUnit, long?, int, int)[] grants =
            [
                (DemoUsage.TextGeneration, DemoUsage.Token, 1_500_000, -HISTORY_DAYS, -90),
                (DemoUsage.TextGeneration, DemoUsage.Token, 6_000_000, -90, 275),
                (DemoUsage.TextGeneration, DemoUsage.Token, 500_000, -30, 10),
                (DemoUsage.TextGeneration, DemoUsage.Token, 6_000_000, 20, 385),
                (DemoUsage.Embeddings, DemoUsage.Token, null, -120, 245),
                (DemoUsage.ImageGeneration, DemoUsage.Image, 1_000, -HISTORY_DAYS, 185),
            ];
            foreach (var (operation, unit, quantity, fromDays, toDays) in grants)
            {
                await grantService.CreateGrantAsync(
                    new CreateGrantRequest(
                        DemoUsage.AccountId,
                        operation,
                        unit,
                        quantity,
                        today.AddDays(fromDays),
                        today.AddDays(toDays)
                    )
                );
            }
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
            $"Seeded 6 grants and {requests:N0} model requests over {HISTORY_DAYS} days."
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
