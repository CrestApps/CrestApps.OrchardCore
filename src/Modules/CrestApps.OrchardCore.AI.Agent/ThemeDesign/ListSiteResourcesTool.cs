using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Lists the published and drafted site stylesheets and scripts.
/// </summary>
public sealed class ListSiteResourcesTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "listSiteResources";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {

      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Lists the site's own stylesheets and scripts: the published ones merged with the changes in the user's theme draft.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var resources = await GetDraftService(services).ListResourcesAsync();

        return Serialize(new { resources });
    }
}
