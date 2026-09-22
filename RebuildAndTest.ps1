dotnet tool restore
dotnet csharpier format .
dotnet clean Corely.Billing.slnx --verbosity minimal
dotnet build Corely.Billing.slnx --verbosity minimal
dotnet test --solution Corely.Billing.slnx --coverage
