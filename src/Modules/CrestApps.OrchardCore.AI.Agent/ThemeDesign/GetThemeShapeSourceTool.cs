using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Reads the template the active theme renders a shape with, so a new template can start from its markup.
/// </summary>
public sealed class GetThemeShapeSourceTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "getThemeShapeSource";

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
        "shapeType": {
          "type": "string",
          "description": "The shape type or alternate, as listThemeShapes returns it, such as Layout or Content__BlogPost."
        }
      },
      "additionalProperties": false,
      "required": ["shapeType"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Returns the source of the template the active theme or a module renders a shape with (Razor or Liquid), so you can write a Liquid template that keeps its structure and data. Razor must be translated to Liquid before it is saved as a template.";

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

        if (!arguments.TryGetFirstString("shapeType", out var shapeType))
        {
            return "The shapeType argument is required.";
        }

        var source = await services.GetRequiredService<IThemeShapeExplorer>().GetShapeSourceAsync(scope, shapeType);

        return source is null
            ? $"The active theme renders no shape named '{shapeType}'. Use listThemeShapes to find the name."
            : Serialize(source);
    }
}
