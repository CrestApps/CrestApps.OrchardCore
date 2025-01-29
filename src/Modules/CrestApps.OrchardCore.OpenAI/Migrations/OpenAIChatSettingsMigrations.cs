using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.OpenAI.Migrations;

/// <summary>
/// This migration was added to add default settings to the existing profiles.
/// </summary>
public sealed class OpenAIChatSettingsMigrations : DataMigration
{
    private readonly IOpenAIChatProfileStore _store;

    public OpenAIChatSettingsMigrations(IOpenAIChatProfileStore store)
    {
        _store = store;
    }

    public async Task<int> CreateAsync()
    {
        var profiles = await _store.GetAllAsync();

        foreach (var profile in profiles)
        {
            if (profile.TryGetSettings<OpenAIChatProfileSettings>(out _))
            {
                // Is settings already exists, no need to migrate it.
                continue;
            }

            profile.WithSettings(new OpenAIChatProfileSettings
            {
                // By default all chat profiles were visible on the admin menu.
                IsOnAdminMenu = profile.Type == OpenAIChatProfileType.Chat,
            });

            await _store.SaveAsync(profile);
        }

        return 1;
    }
}
