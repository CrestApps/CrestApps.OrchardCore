using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Turns the preview of the user's theme draft on or off.
/// </summary>
public sealed class SetThemeDesignPreviewTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "setThemeDesignPreview";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "enabled": {
          "type": "boolean",
          "description": "True to show the draft, false to show the published site."
        }
      },
      "additionalProperties": false,
      "required": ["enabled"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Turns on or off whether the user sees their theme draft while browsing the site. The draft is kept either way.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirst<bool>("enabled", out var enabled))
        {
            return "The enabled argument is required.";
        }

        var result = await GetDraftService(services).SetPreviewAsync(enabled);

        return Report(result, enabled
            ? "The user sees the draft while browsing the site."
            : "The user sees the published site while browsing. The draft is kept.");
    }
}
