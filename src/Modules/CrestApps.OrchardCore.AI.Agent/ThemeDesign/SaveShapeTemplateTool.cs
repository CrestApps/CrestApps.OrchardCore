using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Adds a Liquid shape template to the user's theme draft or changes it there.
/// </summary>
public sealed class SaveShapeTemplateTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "saveShapeTemplate";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "scope": {
          "type": "string",
          "enum": ["site", "admin"],
          "description": "'site' for the front end (the default), 'admin' for the admin."
        },
        "name": {
          "type": "string",
          "description": "The shape the template renders, such as Layout, Content__BlogPost or Content_Summary__Article."
        },
        "content": {
          "type": "string",
          "description": "The complete Liquid template."
        },
        "description": {
          "type": "string",
          "description": "What the template is for."
        },
        "dropThemeResources": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Only for a Layout: names of stylesheets or scripts the theme's own Layout loads that this Layout leaves out on purpose. A Layout that loads bootstrap itself (a new theme) may leave them out without naming them. For a restyle, every resource the theme's Layout loads that is not named here must stay."
        }
      },
      "additionalProperties": false,
      "required": ["name", "content"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Adds a front-end or admin shape template to the user's theme draft, or replaces it there. The template replaces the shape with the same name once published; until then only the user sees it while previewing. The Liquid is checked before it is saved; fix and save again when it reports errors. Send the whole template every time.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var scope = GetScope(arguments);

        if (!arguments.TryGetFirstString("name", out var name) || !arguments.TryGetFirstString("content", out var content))
        {
            return "The name and content arguments are required.";
        }

        arguments.TryGetFirstString("description", out var description);

        // A layout that stops loading the theme's own stylesheets and scripts leaves every menu, pager and content
        // template unstyled. Models do it often and ignore warnings about it, so it must be a deliberate choice.
        var droppedOnPurpose = false;
        string[] droppedByNewTheme = [];

        if (ThemeTemplateReview.IsLayout(name))
        {
            var themeLayout = (await services.GetRequiredService<IThemeShapeExplorer>().GetShapeSourceAsync(scope, "Layout"))?.Content;

            arguments.TryGetFirst<List<string>>("dropThemeResources", out var allowedDrops);

            var dropped = ThemeTemplateReview.GetDroppedResources(themeLayout, content)
                .Where(resource => allowedDrops?.Contains(resource, StringComparer.OrdinalIgnoreCase) != true)
                .ToArray();

            // A layout that loads Bootstrap itself is building a new theme on a clean base: what it leaves out of the
            // theme's own layout is left out on purpose, and is only pointed out.
            var buildsNewTheme = ThemeTemplateReview.GetResourceNames(content).Contains("bootstrap", StringComparer.OrdinalIgnoreCase);

            if (dropped.Length > 0 && buildsNewTheme)
            {
                droppedByNewTheme = dropped;
            }
            else if (dropped.Length > 0)
            {
                return "The change was not made: the theme's own Layout loads " +
                    string.Join(", ", dropped.Select(resource => $"'{resource}'")) +
                    ", and this Layout doesn't, which leaves the theme's menus, pagers and content templates unstyled. For a restyle, add these lines back where the theme has them: " +
                    string.Join(" ", dropped.Select(resource => ThemeTemplateReview.GetLiquidTag(themeLayout, resource))) +
                    " For a new theme, load {% style name: \"bootstrap\" %} and {% script name: \"bootstrap\", at: \"Foot\" %} instead (then the theme's own stylesheets may go), replace the content templates the page preview lists with your own class names, and write a complete stylesheet.";
            }

            droppedOnPurpose = allowedDrops is { Count: > 0 };
        }

        var result = await GetDraftService(services).SaveTemplateAsync(scope, name, content, description);
        var message = Report(result, $"The {(scope == ThemeDesignScope.Admin ? "admin" : "front-end")} template '{name}' is saved in the draft. The user sees it while previewing; it goes live when the draft is published.");

        if (!result.Succeeded)
        {
            return message;
        }

        // The template is valid; point out what would still make the theme look broken, so it is fixed now.
        var warnings = new List<string>();

        if (droppedOnPurpose && !ThemeTemplateReview.GetResourceNames(content).Contains("bootstrap", StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add("This Layout drops the theme's stylesheets without loading Bootstrap, and the menu, pagers and forms render Bootstrap markup (the menu stays hidden without it). Add {% style name: \"bootstrap\" %} and {% script name: \"bootstrap\", at: \"Foot\" %}.");
        }

        if (!ThemeTemplateReview.IsLayout(name))
        {
            var (shapes, _) = await services.GetRequiredService<IThemeShapeExplorer>().ListShapesAsync(scope, name + "__", 500);
            var winners = shapes
                .Where(shape => shape.ShapeType.StartsWith(name + "__", StringComparison.OrdinalIgnoreCase) && !shape.HasDraftTemplate && !shape.HasPublishedTemplate)
                .Select(shape => shape.ShapeType)
                .Take(8)
                .ToArray();

            if (winners.Length > 0)
            {
                warnings.Add($"The theme has more specific templates that win over '{name}' wherever they apply: {string.Join(", ", winners)}. On those pages your template doesn't show. Replace the ones on the pages you are changing, under exactly those names.");
            }
        }

        if (droppedByNewTheme.Length > 0)
        {
            warnings.Add($"Saved as a new theme: this Layout loads Bootstrap and leaves out the theme's {string.Join(", ", droppedByNewTheme.Select(resource => $"'{resource}'"))}. The theme's content templates that still render (see the page preview) lose their styling, so replace them, and add {{% style name: \"font-awesome\", version: \"7\" %}} if you use icons.");
        }

        var inlineStyles = ThemeTemplateReview.CountInlineStyles(content);

        if (inlineStyles > ThemeTemplateReview.MaxInlineStyles)
        {
            warnings.Add($"The template has {inlineStyles} inline style attributes. Give those elements class names and move the styles into the site stylesheet, so the design stays in one place.");
        }

        return warnings.Count == 0
            ? message
            : message + " Warnings to fix now: " + string.Join(" ", warnings);
    }
}
