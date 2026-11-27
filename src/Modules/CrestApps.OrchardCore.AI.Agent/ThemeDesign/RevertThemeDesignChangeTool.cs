using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Drops one change from the user's theme draft.
/// </summary>
public sealed class RevertThemeDesignChangeTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "revertThemeDesignChange";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "kind": {
          "type": "string",
          "enum": ["SiteTemplate", "AdminTemplate", "SiteResource"],
          "description": "What the change is about."
        },
        "name": {
          "type": "string",
          "description": "The template or resource name."
        }
      },
      "additionalProperties": false,
      "required": ["kind", "name"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Drops one change from the user's theme draft, so the published template or resource is left as it is.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("kind", out var kindValue) ||
            !Enum.TryParse<ThemeDesignChangeKind>(kindValue, true, out var kind) ||
            !arguments.TryGetFirstString("name", out var name))
        {
            return "The kind and name arguments are required.";
        }

        var result = await GetDraftService(services).RevertAsync(kind, name);

        return Report(result, $"The change to '{name}' is dropped from the draft.");
    }
}
