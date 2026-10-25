using Corely.Billing;
using Corely.Billing.Demos.Subscription;
using Corely.Billing.Grants.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var connectionString =
    builder.Configuration.GetConnectionString("Billing")
    ?? throw new InvalidOperationException("ConnectionStrings:Billing is not configured");
builder.Services.AddBillingServices(
    BillingOptions
        .Create(builder.Configuration, _ => new BillingSqlServerConfiguration(connectionString))
        .RegisterUnit(Membership.Member.Value, "member")
        .RegisterOperation(
            Membership.Access.Value,
            "Members area",
            GrantKind.Capacity,
            Membership.Member.Value
        )
);
builder.Services.AddScoped<Membership>();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapStaticAssets();
app.MapRazorPages();

app.Run();
