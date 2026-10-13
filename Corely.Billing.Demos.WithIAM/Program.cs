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
        .RegisterOperation(DemoUsage.TextGeneration.Value, "Text generation")
        .RegisterUnit(DemoUsage.Token.Value, "token")
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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(
    async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        context.Response.OnStarting(() =>
        {
            if (headers.CacheControl.Count == 0)
            {
                headers.CacheControl = "no-store";
            }
            return Task.CompletedTask;
        });
        headers.ContentSecurityPolicy =
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
