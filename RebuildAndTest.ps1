dotnet tool restore
dotnet csharpier format .
dotnet clean Corely.Billing.slnx --verbosity minimal
dotnet build Corely.Billing.slnx --verbosity minimal
dotnet publish Corely.Billing.DataAccessMigrations.Cli\Corely.Billing.DataAccessMigrations.Cli.csproj -c Release -r win-x64 -p:DebugType=none
dotnet test --solution Corely.Billing.slnx --coverage
