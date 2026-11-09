using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Stores;

/// <summary>
/// Reads the store profile from the site's settings, for the settings that fall back to it.
/// </summary>
public static class StoreProfileSiteServiceExtensions
{
    /// <summary>
    /// Returns the store's profile as it was saved, or an empty one. Never <see langword="null"/>.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public static async Task<StoreProfileSettings> GetStoreProfileAsync(this ISiteService siteService)
    {
        ArgumentNullException.ThrowIfNull(siteService);

        var site = await siteService.GetSiteSettingsAsync();

        return site?.GetOrCreate<StoreProfileSettings>() ?? new StoreProfileSettings();
    }
}
