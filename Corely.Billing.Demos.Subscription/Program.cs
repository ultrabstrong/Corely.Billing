using Corely.Billing;
using Corely.Billing.Demos.Subscription;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var connectionString =
    builder.Configuration.GetConnectionString("Billing")
    ?? throw new InvalidOperationException("ConnectionStrings:Billing is not configured");
builder.Services.AddBillingServices(
    BillingOptions
        .Create(builder.Configuration, _ => new BillingSqlServerConfiguration(connectionString))
        .RegisterOperation(Membership.Access.Value, "Members area")
        .RegisterUnit(Membership.Visit.Value, "visit")
);
builder.Services.AddScoped<Membership>();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapStaticAssets();
app.MapRazorPages();

app.Run();
