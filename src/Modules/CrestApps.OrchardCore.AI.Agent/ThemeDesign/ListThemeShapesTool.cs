using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Lists the shapes the active theme renders, so the AI knows the names a template can replace.
/// </summary>
public sealed class ListThemeShapesTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "listThemeShapes";

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
        "search": {
          "type": "string",
          "description": "Text the shape name or its file must contain, such as 'Content' or 'BlogPost'."
        },
        "take": {
          "type": "integer",
          "minimum": 1,
          "maximum": 500,
          "description": "The most shapes to return. 100 by default."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Lists the shape types and alternates the active front-end or admin theme can render, with the file that renders each one today, and whether a published or drafted template replaces it. A template named after one of these shapes replaces it. Filter with search, such as Layout, Content, Menu or a content type name.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var scope = GetScope(arguments);

        if (!await CanAsync(services, GetTemplatesPermission(scope)))
        {
            return "You aren't allowed to manage these templates.";
        }

        arguments.TryGetFirstString("search", out var search);

        if (!arguments.TryGetFirst<int>("take", out var take) || take <= 0)
        {
            take = 100;
        }

        var (shapes, total) = await services.GetRequiredService<IThemeShapeExplorer>().ListShapesAsync(scope, search, take);

        return Serialize(new { scope, total, returned = shapes.Count, shapes });
    }
}
