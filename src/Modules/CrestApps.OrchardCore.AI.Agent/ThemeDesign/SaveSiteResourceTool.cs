using System.Text.Json;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Adds a stylesheet or script to the user's theme draft or changes it there.
/// </summary>
public sealed class SaveSiteResourceTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "saveSiteResource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The unique resource name: letters, digits, dots, dashes and underscores, such as site-theme."
        },
        "type": {
          "type": "string",
          "enum": ["Stylesheet", "Script"],
          "description": "Whether it is CSS or JavaScript. Required for a new resource."
        },
        "content": {
          "type": "string",
          "description": "The complete CSS or JavaScript. Required for a new resource."
        },
        "description": {
          "type": "string",
          "description": "What the resource is for."
        },
        "target": {
          "type": "string",
          "enum": ["Frontend", "Admin", "Everywhere"],
          "description": "The pages it is offered to. Frontend by default."
        },
        "themes": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Identifiers of the themes the resource is limited to, such as TheBlogTheme (from getThemeDesignOverview). Empty for every theme."
        },
        "placement": {
          "type": "string",
          "enum": ["Global", "OnDemand"],
          "description": "Global adds it to every page of its target (the default); OnDemand only where a template asks for it."
        },
        "location": {
          "type": "string",
          "enum": ["Foot", "Head"],
          "description": "Where a script goes. Foot by default. Stylesheets always go in the head."
        },
        "dependencies": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Resources that must be on the page first, such as bootstrap or jQuery."
        },
        "position": {
          "type": "integer",
          "description": "The order resources added to every page are added, smallest first."
        },
        "minify": {
          "type": "boolean",
          "description": "Whether it is served minified. True by default."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether it is offered to pages. True by default."
        }
      },
      "additionalProperties": false,
      "required": ["name"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Adds a site stylesheet or script to the user's theme draft, or changes it there, matched by name. When it exists, only the arguments you give change. A Global resource is added to every page of its target; an OnDemand one only where a template asks for it by name. The content is minified; fix and save again when it reports errors.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "The name argument is required.";
        }

        var draftService = GetDraftService(services);
        var existing = await draftService.GetResourceAsync(name);
        var resource = existing?.Draft ?? existing?.Published ?? new SiteResource { Name = name.Trim() };

        if (arguments.TryGetFirstString("type", out var type))
        {
            resource.Type = string.Equals(type, "Script", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "js", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "javascript", StringComparison.OrdinalIgnoreCase)
                ? SiteResourceType.Script
                : SiteResourceType.Stylesheet;
        }

        if (arguments.TryGetFirstString("content", out var content))
        {
            resource.Content = content;
        }

        if (arguments.TryGetFirstString("description", out var description))
        {
            resource.Description = description;
        }

        if (arguments.TryGetFirstString("target", out var target) && Enum.TryParse<SiteResourceTarget>(target, true, out var parsedTarget))
        {
            resource.Target = parsedTarget;
        }

        if (arguments.TryGetFirstString("placement", out var placement) && Enum.TryParse<SiteResourcePlacement>(placement, true, out var parsedPlacement))
        {
            resource.Placement = parsedPlacement;
        }

        if (arguments.TryGetFirstString("location", out var location) && Enum.TryParse<SiteResourceLocation>(location, true, out var parsedLocation))
        {
            resource.Location = parsedLocation;
        }

        if (arguments.TryGetFirst<List<string>>("themes", out var themes))
        {
            resource.Themes = themes ?? [];
        }

        if (arguments.TryGetFirst<List<string>>("dependencies", out var dependencies))
        {
            resource.Dependencies = dependencies ?? [];
        }

        if (arguments.TryGetFirst<int>("position", out var position))
        {
            resource.Position = position;
        }

        if (arguments.TryGetFirst<bool>("minify", out var minify))
        {
            resource.Minify = minify;
        }

        if (arguments.TryGetFirst<bool>("enabled", out var enabled))
        {
            resource.Enabled = enabled;
        }

        var result = await draftService.SaveResourceAsync(resource);

        return Report(result, $"The {(resource.Type == SiteResourceType.Script ? "script" : "stylesheet")} '{resource.Name}' is saved in the draft. The user sees it while previewing; it goes live when the draft is published.");
    }
}
