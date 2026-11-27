using System.Text.Encodings.Web;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Renders the shapes the viewer's theme draft changes with the draft's templates. It runs before Orchard Core's
/// template resolvers, so a drafted template wins over a published one, and a template the draft removes falls back
/// to the theme's own.
/// </summary>
internal sealed class ThemeDesignPreviewShapeBindingResolver : IShapeBindingResolver
{
    private static readonly ShapeBinding _emptyBinding = new()
    {
        BindingName = "ThemeDraft.Removed",
        BindingSource = "ThemeDraft",
        BindingAsync = _ => Task.FromResult<IHtmlContent>(HtmlString.Empty),
    };

    private readonly ThemeDesignPreview _preview;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IThemeManager _themeManager;
    private readonly IShapeTableManager _shapeTableManager;
    private readonly HtmlEncoder _htmlEncoder;

    private ThemeDesignScope? _scope;
    private ShapeTable _themeShapeTable;

    public ThemeDesignPreviewShapeBindingResolver(
        ThemeDesignPreview preview,
        IHttpContextAccessor httpContextAccessor,
        ILiquidTemplateManager liquidTemplateManager,
        IThemeManager themeManager,
        IShapeTableManager shapeTableManager,
        HtmlEncoder htmlEncoder)
    {
        _preview = preview;
        _httpContextAccessor = httpContextAccessor;
        _liquidTemplateManager = liquidTemplateManager;
        _themeManager = themeManager;
        _shapeTableManager = shapeTableManager;
        _htmlEncoder = htmlEncoder;
    }

    public async Task<ShapeBinding> GetShapeBindingAsync(string shapeType)
    {
        // Called for every alternate of every shape, so the draft and the side of the site are worked out once.
        var draft = await _preview.GetDraftAsync();

        if (draft is null || draft.Templates.Count == 0)
        {
            return null;
        }

        _scope ??= AdminAttribute.IsApplied(_httpContextAccessor.HttpContext) ? ThemeDesignScope.Admin : ThemeDesignScope.Site;

        var template = draft.FindTemplate(_scope.Value, shapeType);

        if (template is null)
        {
            return null;
        }

        if (!template.Removed)
        {
            var content = template.Content;

            return new ShapeBinding
            {
                BindingName = shapeType,
                BindingSource = $"ThemeDraft/{shapeType}.liquid",
                BindingAsync = displayContext => _liquidTemplateManager.RenderHtmlContentAsync(content, _htmlEncoder, displayContext.Value),
            };
        }

        // The draft removes the published template: show what the theme renders without it.
        if (_themeShapeTable is null)
        {
            var theme = await _themeManager.GetThemeAsync();
            _themeShapeTable = await _shapeTableManager.GetShapeTableAsync(theme?.Id);
        }

        return _themeShapeTable.Bindings.TryGetValue(shapeType, out var themeBinding)
            ? themeBinding
            : _emptyBinding;
    }
}
