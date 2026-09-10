using eSureHi.Data;
using eSureHi.Services;
using Microsoft.EntityFrameworkCore;

eSureHi.App.DbConfig = DatabaseConfiguration.Load();

if (!eSureHi.App.DbConfig.IsConfigured)
{
    Console.Error.WriteLine("Database configuration is incomplete.");
    return 1;
}

try
{
    using var db = eSureHiDbContextFactory.CreateCloud();
    await db.Database.OpenConnectionAsync();
    await db.Database.CloseConnectionAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Cannot connect to the configured database. Error: {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

await AuthService.Instance.SeedAdminAsync();
await AuthService.Instance.SeedWebFlowAccountsAsync();
await AuthService.Instance.SeedDefaultEmployeeAsync();
await AuthService.Instance.SeedDocumentTypesAsync();

Console.WriteLine("Essential master data seed completed.");
return 0;
