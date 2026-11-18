using CrestApps.OrchardCore.AdminTheme.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AdminTheme.Migrations;

/// <summary>
/// Seeds the theme's default color schemes once, when the theme is first enabled (or on the first start of a site
/// that had it enabled before schemes were records). Admins edit and delete them from there; they are never seeded
/// again, so a deleted default stays deleted.
/// </summary>
internal sealed class AdminColorSchemesMigrations : DataMigration
{
    private readonly AdminColorSchemeStore _store;

    public AdminColorSchemesMigrations(AdminColorSchemeStore store)
    {
        _store = store;
    }

    public async Task<int> CreateAsync()
    {
        await _store.SeedDefaultsAsync();

        return 1;
    }
}
