using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Removes a shape template in the user's theme draft.
/// </summary>
public sealed class RemoveShapeTemplateTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "removeShapeTemplate";

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
          "description": "The template name."
        }
      },
      "additionalProperties": false,
      "required": ["name"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Marks a published front-end or admin template for removal in the user's theme draft, so the theme's own template is used again once the draft is published. A template the draft added is simply dropped.";

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

        var result = await GetDraftService(services).RemoveTemplateAsync(scope, name);

        return Report(result, $"The template '{name}' is removed in the draft.");
    }
}
