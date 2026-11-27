using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Reads a shape template as it is published and as the draft has it.
/// </summary>
public sealed class GetShapeTemplateTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "getShapeTemplate";

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
          "description": "The template name: the shape it renders, such as Layout."
        }
      },
      "additionalProperties": false,
      "required": ["name"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Returns a front-end or admin shape template's Liquid as it is published and as the user's theme draft has it.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var scope = GetScope(arguments);

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "The name argument is required.";
        }

        var template = await GetDraftService(services).GetTemplateAsync(scope, name);

        return template is null
            ? $"There is no published or drafted template named '{name}'. Use getThemeShapeSource to read the theme's own template."
            : Serialize(template);
    }
}
