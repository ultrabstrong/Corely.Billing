using System.Runtime.CompilerServices;

// BillingDbContext is internal to Corely.Billing, so the factory that returns one cannot be public.
[assembly: InternalsVisibleTo("Corely.Billing.DataAccessMigrations.Cli")]
