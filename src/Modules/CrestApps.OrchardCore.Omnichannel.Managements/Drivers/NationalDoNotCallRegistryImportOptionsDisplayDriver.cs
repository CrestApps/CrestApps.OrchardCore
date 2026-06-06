using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Display driver that adds the national do-not-call registry import option to the import form.
/// This driver is only registered when the NationalDoNotCallRegistry feature is enabled.
/// </summary>
public sealed class NationalDoNotCallRegistryImportOptionsDisplayDriver : DisplayDriver<ImportContent>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<INationalDoNotCallRegistry> _registries;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NationalDoNotCallRegistryImportOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="registries">The available do-not-call registries.</param>
    /// <param name="siteService">The site service.</param>
    public NationalDoNotCallRegistryImportOptionsDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<INationalDoNotCallRegistry> registries,
        ISiteService siteService)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _registries = registries;
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> EditAsync(ImportContent model, BuildEditorContext context)
    {
        if (!await IsOmnichannelContactAsync(model))
        {
            return null;
        }

        var settings = await GetSettingsAsync();
        var options = model.GetOrCreate<OmnichannelContactImportOptionsPart>();

        return Initialize<NationalDoNotCallRegistryImportOptionsViewModel>("NationalDoNotCallRegistryImportOptions_Edit", viewModel =>
        {
            viewModel.IsGloballyEnforced = settings.EnforceGlobally;
            viewModel.IgnoreDoNotCallNumbers = settings.EnforceGlobally || options.IgnoreDoNotCallNumbers;
            viewModel.SelectedRegistryKeys = GetEffectiveRegistryKeys(options, settings);
            viewModel.AvailableRegistries = _registries.Select(r => new NationalDoNotCallRegistryEntry
            {
                Key = r.Key,
                DisplayName = r.DisplayName,
                Description = r.Description,
                IsEnforced = settings.EnforcedRegistryKeys?.Contains(r.Key) == true,
            }).ToArray();
        }).Location("Content:6");
    }

    public override async Task<IDisplayResult> UpdateAsync(ImportContent model, UpdateEditorContext context)
    {
        if (!await IsOmnichannelContactAsync(model))
        {
            return null;
        }

        var viewModel = new NationalDoNotCallRegistryImportOptionsViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix))
        {
            var settings = await GetSettingsAsync();
            var options = model.GetOrCreate<OmnichannelContactImportOptionsPart>();

            options.IgnoreDoNotCallNumbers = settings.EnforceGlobally || viewModel.IgnoreDoNotCallNumbers;
            options.SelectedRegistryKeys = GetEffectiveRegistryKeys(viewModel, settings);

            model.Put(options);
        }

        return await EditAsync(model, context);
    }

    private static string[] GetEffectiveRegistryKeys(OmnichannelContactImportOptionsPart options, DncRegistrySettings settings)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (settings.EnforcedRegistryKeys != null)
        {
            foreach (var key in settings.EnforcedRegistryKeys)
            {
                keys.Add(key);
            }
        }

        if (options.SelectedRegistryKeys != null)
        {
            foreach (var key in options.SelectedRegistryKeys)
            {
                keys.Add(key);
            }
        }

        return [.. keys];
    }

    private static string[] GetEffectiveRegistryKeys(NationalDoNotCallRegistryImportOptionsViewModel viewModel, DncRegistrySettings settings)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (settings.EnforcedRegistryKeys != null)
        {
            foreach (var key in settings.EnforcedRegistryKeys)
            {
                keys.Add(key);
            }
        }

        if (viewModel.SelectedRegistryKeys != null)
        {
            foreach (var key in viewModel.SelectedRegistryKeys)
            {
                keys.Add(key);
            }
        }

        return [.. keys];
    }

    private async Task<DncRegistrySettings> GetSettingsAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return site.GetOrCreate<DncRegistrySettings>();
    }

    private async Task<bool> IsOmnichannelContactAsync(ImportContent model)
    {
        if (string.IsNullOrEmpty(model.ContentTypeId))
        {
            return false;
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentTypeId);

        return contentTypeDefinition?.Parts?.Any(p =>
            p.PartDefinition.Name == OmnichannelConstants.ContentParts.OmnichannelContact) == true;
    }
}
