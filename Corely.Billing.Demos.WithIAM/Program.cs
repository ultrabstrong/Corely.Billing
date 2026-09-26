using Corely.Billing;
using Corely.Billing.Demos.WithIAM;
using Corely.Billing.Demos.WithIAM.Components;
using Corely.Billing.IAM.Extensions;
using Corely.Billing.Web;
using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM;
using Corely.IAM.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddIAMWeb();
builder.Services.AddIAMWebBlazor();

var connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");

builder.Services.AddIAMServices(
    IAMOptions
        .Create(
            builder.Configuration,
            new DemoSecurityConfigurationProvider(builder.Configuration),
            _ => new SqlServerConfiguration(connectionString)
        )
        .RegisterBillingResourceTypes()
);

builder.Services.AddBillingServices(
    BillingOptions
        .Create(builder.Configuration, _ => new SqlServerConfiguration(connectionString))
        .RegisterOperation(DemoUsage.Extraction.Value, "Document extraction")
        .RegisterUnit(DemoUsage.Page.Value, "page")
        .UseCorelyIamPermissions()
);
builder.Services.AddBillingWebIam();
builder.Services.AddScoped<UsageSimulator>();

var app = builder.Build();

if (args.Contains("--seed"))
{
    await DemoSeed.RunAsync(app.Services);
    return;
}

app.Use(
    async (context, next) =>
    {
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; "
            + "connect-src 'self' wss: ws:; img-src 'self' data:; font-src 'self'; "
            + "frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";
        await next();
    }
);
app.UseIAMWebAuthentication();
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(BillingWebRoutes).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
