using CrestApps.OrchardCore.ThemeDesigner.Models;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Descriptors.ShapeTemplateStrategy;
using OrchardCore.Documents;
using OrchardCore.Environment.Extensions;
using OrchardCore.Settings;
using OrchardCore.Templates.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Reads the active themes' shape tables and template files.
/// </summary>
internal sealed class ThemeShapeExplorer : IThemeShapeExplorer
{
    // The longest template source returned, so one large file can't fill the AI's context.
    private const int MaxSourceLength = 64 * 1024;

    private readonly ISiteService _siteService;
    private readonly IAdminThemeService _adminThemeService;
    private readonly IExtensionManager _extensionManager;
    private readonly IShapeTableManager _shapeTableManager;
    private readonly IShapeTemplateFileProviderAccessor _fileProviderAccessor;
    private readonly IDocumentManager<TemplatesDocument> _templatesManager;
    private readonly IDocumentManager<AdminTemplatesDocument> _adminTemplatesManager;
    private readonly IThemeDesignDraftService _draftService;

    public ThemeShapeExplorer(
        ISiteService siteService,
        IAdminThemeService adminThemeService,
        IExtensionManager extensionManager,
        IShapeTableManager shapeTableManager,
        IShapeTemplateFileProviderAccessor fileProviderAccessor,
        IDocumentManager<TemplatesDocument> templatesManager,
        IDocumentManager<AdminTemplatesDocument> adminTemplatesManager,
        IThemeDesignDraftService draftService)
    {
        _siteService = siteService;
        _adminThemeService = adminThemeService;
        _extensionManager = extensionManager;
        _shapeTableManager = shapeTableManager;
        _fileProviderAccessor = fileProviderAccessor;
        _templatesManager = templatesManager;
        _adminTemplatesManager = adminTemplatesManager;
        _draftService = draftService;
    }

    public async Task<ActiveThemes> GetActiveThemesAsync()
    {
        var siteThemeId = await GetThemeIdAsync(ThemeDesignScope.Site);
        var adminThemeId = await GetThemeIdAsync(ThemeDesignScope.Admin);

        return new ActiveThemes
        {
            SiteThemeId = siteThemeId,
            SiteThemeName = GetThemeName(siteThemeId),
            AdminThemeId = adminThemeId,
            AdminThemeName = GetThemeName(adminThemeId),
        };
    }

    public async Task<(IReadOnlyList<ThemeShapeInfo> Shapes, int Total)> ListShapesAsync(ThemeDesignScope scope, string search, int take)
    {
        var shapeTable = await _shapeTableManager.GetShapeTableAsync(await GetThemeIdAsync(scope));
        var published = scope == ThemeDesignScope.Admin
            ? await _adminTemplatesManager.GetOrCreateImmutableAsync()
            : (TemplatesDocument)await _templatesManager.GetOrCreateImmutableAsync();
        var draft = await _draftService.GetDraftAsync();

        var matches = shapeTable.Bindings
            .Where(binding => string.IsNullOrWhiteSpace(search) ||
                binding.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                binding.Value.BindingSource?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(binding => binding.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var shapes = matches
            .Take(Math.Clamp(take, 1, 500))
            .Select(binding => new ThemeShapeInfo
            {
                ShapeType = binding.Key,
                BindingSource = binding.Value.BindingSource,
                HasPublishedTemplate = published.Templates.ContainsKey(binding.Key),
                HasDraftTemplate = draft?.FindTemplate(scope, binding.Key) is not null,
            })
            .ToArray();

        return (shapes, matches.Length);
    }

    public async Task<ThemeShapeSource> GetShapeSourceAsync(ThemeDesignScope scope, string shapeType)
    {
        if (string.IsNullOrWhiteSpace(shapeType))
        {
            return null;
        }

        var shapeTable = await _shapeTableManager.GetShapeTableAsync(await GetThemeIdAsync(scope));

        if (!shapeTable.Bindings.TryGetValue(shapeType, out var binding))
        {
            return null;
        }

        var source = new ThemeShapeSource
        {
            ShapeType = shapeType,
            BindingSource = binding.BindingSource,
            Language = "code",
        };

        var path = binding.BindingSource;

        if (string.IsNullOrEmpty(path))
        {
            return source;
        }

        if (path.EndsWith(".liquid", StringComparison.OrdinalIgnoreCase))
        {
            source.Language = "liquid";
        }
        else if (path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            source.Language = "razor";
        }
        else
        {
            return source;
        }

        var file = _fileProviderAccessor.FileProvider.GetFileInfo(path);

        if (file.Exists && !file.IsDirectory)
        {
            await using var stream = file.CreateReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            source.Content = content.Length > MaxSourceLength
                ? content[..MaxSourceLength]
                : content;
        }

        return source;
    }

    private async Task<string> GetThemeIdAsync(ThemeDesignScope scope)
    {
        if (scope == ThemeDesignScope.Admin)
        {
            return await _adminThemeService.GetAdminThemeNameAsync();
        }

        var site = await _siteService.GetSiteSettingsAsync();

        return site.Properties["CurrentThemeName"]?.GetValue<string>();
    }

    private string GetThemeName(string themeId)
        => string.IsNullOrEmpty(themeId)
            ? null
            : _extensionManager.GetExtension(themeId)?.Manifest?.Name ?? themeId;
}
