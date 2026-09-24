using Corely.Billing;
using Corely.Billing.Demos.Portal;
using Corely.Billing.Demos.Portal.Components;
using Corely.Billing.Web;
using Corely.Billing.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);
var seeding = args.Contains("--seed");
var seedClock = new SeedClock();
if (seeding)
    builder.Services.AddSingleton<TimeProvider>(seedClock);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var connectionString =
    builder.Configuration.GetConnectionString("Billing")
    ?? throw new InvalidOperationException("ConnectionStrings:Billing is not configured");
builder.Services.AddBillingServices(
    DemoUsage.Register(
        BillingOptions.Create(
            builder.Configuration,
            _ => new BillingSqlServerConfiguration(connectionString)
        )
    )
);
builder.Services.AddBillingWeb<DemoAccountAccessor>();
builder.Services.AddScoped<UsageSimulator>();

var app = builder.Build();

if (seeding)
{
    await DemoSeed.RunAsync(app.Services, seedClock);
    return;
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(BillingWebRoutes).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
