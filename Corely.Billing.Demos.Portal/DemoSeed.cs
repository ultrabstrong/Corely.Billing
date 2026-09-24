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

            (UsageOperation, long?, int, int)[] grants =
            [
                (DemoUsage.Extraction, 1_500, -HISTORY_DAYS, -90),
                (DemoUsage.Extraction, 6_000, -90, 275),
                (DemoUsage.Extraction, 500, -30, 10),
                (DemoUsage.Extraction, 6_000, 20, 385),
                (DemoUsage.Summaries, null, -120, 245),
            ];
            foreach (var (operation, quantity, fromDays, toDays) in grants)
            {
                await grantService.CreateGrantAsync(
                    new CreateGrantRequest(
                        DemoUsage.AccountId,
                        operation,
                        DemoUsage.Page,
                        quantity,
                        today.AddDays(fromDays),
                        today.AddDays(toDays)
                    )
                );
            }
        }

        var random = new Random(42);
        var jobs = 0;
        for (var day = -HISTORY_DAYS; day < 0; day++)
        {
            foreach (var (operation, count, maxPages) in DailyWork(random))
            {
                for (var i = 0; i < count; i++)
                {
                    clock.Now = new DateTimeOffset(
                        today.AddDays(day).AddHours(8 + random.Next(10)).AddMinutes(random.Next(60))
                    );
                    using var scope = services.CreateScope();
                    await scope
                        .ServiceProvider.GetRequiredService<UsageSimulator>()
                        .RunAsync(operation, random.Next(1, maxPages));
                    jobs++;
                }
            }
        }

        Console.WriteLine($"Seeded 5 grants and {jobs:N0} units of work over {HISTORY_DAYS} days.");
    }

    private static (UsageOperation, int, int)[] DailyWork(Random random) =>
        [
            (DemoUsage.Extraction, random.Next(0, 4), 30),
            (DemoUsage.Summaries, random.Next(0, 3), 10),
        ];
}
