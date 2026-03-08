using CrestApps.OrchardCore.Recipes.Core.Schemas;
using OrchardCore.Environment.Extensions;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Default implementation that resolves feature and theme IDs from
/// Orchard Core's extension manager.
/// </summary>
public sealed class OrchardFeatureSchemaProvider : IFeatureSchemaProvider
{
    private const string ThemeManifestType = "Theme";
    private readonly IExtensionManager _extensionManager;

    public OrchardFeatureSchemaProvider(IExtensionManager extensionManager)
    {
        _extensionManager = extensionManager;
    }

    public Task<IEnumerable<string>> GetFeatureIdsAsync()
    {
        var ids = _extensionManager.GetFeatures()
            .Select(f => f.Id)
            .Distinct()
            .OrderBy(id => id);

        return Task.FromResult<IEnumerable<string>>(ids);
    }

    public Task<IEnumerable<string>> GetThemeIdsAsync()
    {
        var ids = _extensionManager.GetFeatures()
            .Where(f => string.Equals(f.Extension?.Manifest?.Type, ThemeManifestType, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Id)
            .Distinct()
            .OrderBy(id => id);

        return Task.FromResult<IEnumerable<string>>(ids);
    }
}
