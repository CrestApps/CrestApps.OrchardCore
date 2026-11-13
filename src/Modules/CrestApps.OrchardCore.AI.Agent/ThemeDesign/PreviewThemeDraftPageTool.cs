using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Renders a page of the site as the user, with their theme draft, so the AI sees what its changes look like.
/// </summary>
public sealed class PreviewThemeDraftPageTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "previewThemeDraftPage";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "The path of a page on this site, such as / for the home page or /blog/my-post. Admin pages start with /Admin."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Renders a page of the site as the user sees it with their theme draft and returns its status code, any error, the stylesheets and scripts it loads, the templates that rendered each part of it (with the name a template must take to replace that part), and its body markup (without scripts and styles). Call it after every change to check that the page works and looks as intended, then fix what is wrong. For a new theme it also returns stillToDo: what the theme still draws and whether the stylesheet is too small.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("path", out var path))
        {
            path = "/";
        }

        ThemeDraftPagePreview preview;

        try
        {
            preview = await services.GetRequiredService<IThemeDraftPagePreviewer>().RenderAsync(path.Trim(), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return $"The page could not be requested: {ex.Message}";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "The page took too long to render.";
        }

        if (preview is null)
        {
            return "Give the path of a page on this site, starting with a slash, such as /. The user must be signed in.";
        }

        // A new theme is checked against what it set out to do, so the model knows what is left.
        IReadOnlyList<string> advice = [];
        var scope = preview.Path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ? ThemeDesignScope.Admin : ThemeDesignScope.Site;
        var draftLayout = (await GetDraftService(services).GetDraftAsync())?.FindTemplate(scope, "Layout");

        if (draftLayout is { Removed: false })
        {
            var themeLayout = await services.GetRequiredService<IThemeShapeExplorer>().GetShapeSourceAsync(scope, "Layout");

            if (ThemeTemplateReview.IsNewTheme(themeLayout?.Content, draftLayout.Content))
            {
                advice = ThemeTemplateReview.GetNewThemeAdvice(
                    preview.RenderedTemplates.Select(template => (template.Name, template.Source)),
                    preview.StylesheetSizes.Where(stylesheet => stylesheet.IsSiteResource).Sum(stylesheet => stylesheet.Bytes ?? 0));
            }
        }

        return advice.Count == 0
            ? Serialize(preview)
            : Serialize(new { stillToDo = advice, preview });
    }
}
