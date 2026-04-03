using System.Text.Json;
using CrestApps.AI.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Features;

public sealed class FeaturesSearchTool : AIFunction
{
    public const string TheName = "searchSiteFeature";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "A term used to search for relevant features."
        }
      },
      "additionalProperties": false,
      "required": [
        "name"
      ]

    }

    """);

    public override string Name => TheName;

    public override string Description => "Search for a feature on the site";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {

        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {

        ArgumentNullException.ThrowIfNull(arguments);

        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<FeaturesSearchTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {

            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);

        }

        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();

        if (!arguments.TryGetFirstString("name", out var name))

        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'name' argument.", Name);

            return "Unable to find a name argument in the function arguments.";
        }

        var features = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(feature => !feature.EnabledByDependencyOnly && !feature.IsTheme() && (feature.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || feature.Id.Contains(name, StringComparison.OrdinalIgnoreCase)));

        var enabledFeatureIds = (await shellFeaturesManager.GetEnabledFeaturesAsync())

            .Select(x => x.Id)
            .ToHashSet();

        if (logger.IsEnabled(LogLevel.Debug))
        {

            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return JsonSerializer.Serialize(features.Select(feature => feature.AsAIObject(enabledFeatureIds.Contains(feature.Id))));
    }
}
