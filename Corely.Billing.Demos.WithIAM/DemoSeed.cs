using Corely.Billing.Grants.Models;
using Corely.Billing.IAM;
using Corely.Billing.Services;
using Corely.IAM.Models;
using Corely.IAM.Permissions.Models;
using Corely.IAM.Services;

namespace Corely.Billing.Demos.WithIAM;

internal static class DemoSeed
{
    public const string PASSWORD = "Test1234";
    private const string DEVICE_ID = "demo-seed";

    public static async Task RunAsync(IServiceProvider services)
    {
        var owner = await RegisterUserAsync(services, "olivia", "olivia@example.com");
        if (owner is null)
        {
            Console.WriteLine("Already seeded - olivia exists. Drop the database to reseed.");
            return;
        }
        var member = await RegisterUserAsync(services, "bobby", "bobby@example.com");
        var editor = await RegisterUserAsync(services, "carla", "carla@example.com");

        Guid accountId;
        using (var scope = services.CreateScope())
        {
            await SignInAsync(scope.ServiceProvider, "olivia", accountId: null);
            accountId = (
                await scope
                    .ServiceProvider.GetRequiredService<IRegistrationService>()
                    .RegisterAccountAsync(new RegisterAccountRequest("Acme", owner.Value))
            ).CreatedAccountId;
        }

        using (var scope = services.CreateScope())
        {
            await SignInAsync(scope.ServiceProvider, "olivia", accountId);
            var registration = scope.ServiceProvider.GetRequiredService<IRegistrationService>();
            await registration.RegisterUserWithAccountAsync(
                new RegisterUserWithAccountRequest(member!.Value, accountId)
            );
            await registration.RegisterUserWithAccountAsync(
                new RegisterUserWithAccountRequest(editor!.Value, accountId)
            );
        }

        using (var scope = services.CreateScope())
        {
            scope
                .ServiceProvider.GetRequiredService<IAuthenticationService>()
                .AuthenticateAsSystem(DEVICE_ID);
            await RegisterGrantEditorAsync(scope.ServiceProvider, accountId, editor.Value);

            var grants = scope.ServiceProvider.GetRequiredService<IGrantService>();
            var today = scope
                .ServiceProvider.GetRequiredService<TimeProvider>()
                .GetUtcNow()
                .UtcDateTime.Date;
            await grants.CreateGrantAsync(
                new CreateGrantRequest(
                    accountId,
                    DemoUsage.TextGeneration,
                    DemoUsage.Token,
                    2_000_000,
                    today.AddDays(-30),
                    today.AddDays(335)
                )
            );
            await grants.CreateGrantAsync(
                new CreateGrantRequest(
                    accountId,
                    DemoUsage.TextGeneration,
                    DemoUsage.Token,
                    250_000,
                    today.AddDays(-5),
                    today.AddDays(9)
                )
            );
        }

        using (var scope = services.CreateScope())
        {
            await SignInAsync(scope.ServiceProvider, "olivia", accountId);
            var simulator = scope.ServiceProvider.GetRequiredService<UsageSimulator>();
            var random = new Random(7);
            for (var i = 0; i < 12; i++)
                await simulator.GenerateTextAsync(accountId, random.Next(5_000, 40_000));
        }

        Console.WriteLine(
            "Seeded account Acme: owner olivia who may read grants, carla who may read and update them, "
                + "and bobby with no roles"
        );
        Console.WriteLine($"Password for all three: {PASSWORD}");
    }

    private static async Task RegisterGrantEditorAsync(
        IServiceProvider scoped,
        Guid accountId,
        Guid userId
    )
    {
        var registration = scoped.GetRequiredService<IRegistrationService>();
        var grants = await registration.RegisterPermissionAsync(
            new RegisterPermissionRequest(
                accountId,
                BillingResourceTypes.GRANT_RESOURCE_TYPE,
                Guid.Empty,
                Read: true,
                Update: true,
                Description: "Read and update every grant"
            )
        );
        var permissions = await scoped
            .GetRequiredService<IRetrievalService>()
            .ListPermissionsAsync(new ListPermissionsRequest(accountId, Take: 100));
        var ownerConsumption = permissions.Data!.Items.Single(p =>
            p.IsSystemDefined && p.ResourceType == BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE
        );
        var role = await registration.RegisterRoleAsync(
            new RegisterRoleRequest("Grant editor", accountId)
        );
        await registration.RegisterPermissionsWithRoleAsync(
            new RegisterPermissionsWithRoleRequest(
                [grants.CreatedPermissionId, ownerConsumption.Id],
                role.CreatedRoleId,
                accountId
            )
        );
        await registration.RegisterRolesWithUserAsync(
            new RegisterRolesWithUserRequest([role.CreatedRoleId], userId, accountId)
        );
    }

    private static async Task<Guid?> RegisterUserAsync(
        IServiceProvider services,
        string username,
        string email
    )
    {
        using var scope = services.CreateScope();
        var result = await scope
            .ServiceProvider.GetRequiredService<IRegistrationService>()
            .RegisterUserAsync(new RegisterUserRequest(username, email, PASSWORD));
        return result.ResultCode == RegisterUserResultCode.Success ? result.CreatedUserId : null;
    }

    private static async Task SignInAsync(IServiceProvider scoped, string username, Guid? accountId)
    {
        var result = await scoped
            .GetRequiredService<IAuthenticationService>()
            .SignInAsync(new SignInRequest(username, PASSWORD, DEVICE_ID, accountId));
        if (result.ResultCode != SignInResultCode.Success)
            throw new InvalidOperationException(
                $"Seed sign-in failed for {username}: {result.Message}"
            );
    }
}
