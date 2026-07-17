using eSureHi.Data;
using eSureHi.Services;

eSureHi.App.DbConfig = DatabaseConfiguration.Load();

if (!eSureHi.App.DbConfig.IsConfigured)
{
    Console.Error.WriteLine("Database configuration is incomplete.");
    return 1;
}

if (!await eSureHiDbContextFactory.CanConnectAsync())
{
    Console.Error.WriteLine("Cannot connect to the configured database.");
    return 1;
}

await AuthService.Instance.SeedAdminAsync();
await AuthService.Instance.SeedWebFlowAccountsAsync();
await AuthService.Instance.SeedDefaultEmployeeAsync();
await AuthService.Instance.SeedDocumentTypesAsync();

Console.WriteLine("Essential master data seed completed.");
return 0;
