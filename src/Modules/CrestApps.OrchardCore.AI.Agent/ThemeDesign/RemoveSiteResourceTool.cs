using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Removes a site stylesheet or script in the user's theme draft.
/// </summary>
public sealed class RemoveSiteResourceTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "removeSiteResource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The resource name."
        }
      },
      "additionalProperties": false,
      "required": ["name"]
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Marks a published site stylesheet or script for removal in the user's theme draft. A resource the draft added is simply dropped.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "The name argument is required.";
        }

        var result = await GetDraftService(services).RemoveResourceAsync(name);

        return Report(result, $"The site resource '{name}' is removed in the draft.");
    }
}
