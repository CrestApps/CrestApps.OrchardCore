using System.Text.Json;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Reads a site stylesheet or script as it is published and as the draft has it.
/// </summary>
public sealed class GetSiteResourceTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "getSiteResource";

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
    public override string Description => "Returns a site stylesheet or script, with its content, as it is published and as the user's theme draft has it.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "The name argument is required.";
        }

        var details = await GetDraftService(services).GetResourceAsync(name);

        return details is null
            ? $"There is no published or drafted site resource named '{name}'."
            : Serialize(new
            {
                details.DraftStatus,
                published = Describe(details.Published),
                draft = Describe(details.Draft),
            });
    }

    private static object Describe(SiteResource resource)
        => resource is null
            ? null
            : new
            {
                resource.Name,
                resource.Description,
                resource.Type,
                resource.Target,
                resource.Themes,
                resource.Placement,
                resource.Location,
                resource.Dependencies,
                resource.Position,
                resource.Minify,
                resource.Enabled,
                resource.Content,
            };
}
