using CrestApps.Core;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Migrations;

internal sealed class MemoryMetadataMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var profileStore = scope.ServiceProvider.GetRequiredService<IAIProfileStore>();
            var templateCatalog = scope.ServiceProvider.GetRequiredService<INamedSourceCatalog<AIProfileTemplate>>();

            await MigrateSiteSettingsAsync(siteService);
            await MigrateProfilesAsync(profileStore);
            await MigrateTemplatesAsync(templateCatalog);
        });

        return 1;
    }

    private static async Task MigrateSiteSettingsAsync(ISiteService siteService)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        var legacyValue = site.Properties[MemoryMetadataExtensions.LegacyChatInteractionSettingsKey];

        if (legacyValue is null)
        {
            if (!site.Properties.ContainsKey(nameof(MemoryMetadata)))
            {
                site.Alter<MemoryMetadata>(metadata => metadata.EnableUserMemory = true);
                await siteService.UpdateSiteSettingsAsync(site);
            }

            return;
        }

        if (!MemoryMetadataExtensions.TryDeserialize(legacyValue, out var metadata))
        {
            metadata = new MemoryMetadata { EnableUserMemory = true };
        }

        site.Properties.Remove(MemoryMetadataExtensions.LegacyChatInteractionSettingsKey);
        site.Alter<MemoryMetadata>(memory => memory.EnableUserMemory = metadata.EnableUserMemory ?? true);
        await siteService.UpdateSiteSettingsAsync(site);
    }

    private static async Task MigrateProfilesAsync(IAIProfileStore profileStore)
    {
        foreach (var profile in await profileStore.GetAllAsync())
        {
            if (profile.Has<MemoryMetadata>())
            {
                continue;
            }

            if (!MemoryMetadataExtensions.TryDeserialize(profile.Settings[MemoryMetadataExtensions.LegacyAIProfileSettingsKey], out var metadata) &&
                !MemoryMetadataExtensions.TryDeserialize(profile.Settings[MemoryMetadataExtensions.LegacyMvcMemorySettingsKey], out metadata))
            {
                continue;
            }

            profile.Put(metadata);
            profile.Settings.Remove(MemoryMetadataExtensions.LegacyAIProfileSettingsKey);
            profile.Settings.Remove(MemoryMetadataExtensions.LegacyMvcMemorySettingsKey);

            await profileStore.UpdateAsync(profile);
        }
    }

    private static async Task MigrateTemplatesAsync(INamedSourceCatalog<AIProfileTemplate> templateCatalog)
    {
        foreach (var template in await templateCatalog.GetAllAsync())
        {
            if (template.Has<MemoryMetadata>())
            {
                continue;
            }

            if (!MemoryMetadataExtensions.TryDeserialize(GetPropertyValue(template.Properties, MemoryMetadataExtensions.LegacyAIProfileSettingsKey), out var metadata) &&
                !MemoryMetadataExtensions.TryDeserialize(GetPropertyValue(template.Properties, MemoryMetadataExtensions.LegacyMvcMemorySettingsKey), out metadata))
            {
                continue;
            }

            template.Put(metadata);
            template.Properties.Remove(MemoryMetadataExtensions.LegacyAIProfileSettingsKey);
            template.Properties.Remove(MemoryMetadataExtensions.LegacyMvcMemorySettingsKey);

            await templateCatalog.UpdateAsync(template);
        }
    }

    private static object GetPropertyValue(IDictionary<string, object> properties, string key)
        => properties is not null && properties.TryGetValue(key, out var value)
            ? value
            : null;
}
