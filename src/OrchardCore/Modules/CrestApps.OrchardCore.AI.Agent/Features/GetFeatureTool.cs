using System.Text.Json;
using CrestApps.AI.Extensions;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Features;

public sealed class GetFeatureTool : AIFunction
{
    public const string TheName = "getSiteFeature";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
       """
        {
          "type": "object",
          "properties": {
            "featureId": {
              "type": "string",
              "description": "A unique feature ID to get info for."
            }
          },
          "additionalProperties": false,
          "required": ["featureId"]
        }
        """);

    public override string Name => TheName;

    public override string Description => "Enables feature site features";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<GetFeatureTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();

        if (!arguments.TryGetFirstString("featureId", out var featureId))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'featureId' argument.", Name);

            return "Unable to find a featureId argument in the function arguments.";
        }

        var feature = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .FirstOrDefault(feature => !feature.IsTheme() && feature.Id.Equals(featureId, StringComparison.OrdinalIgnoreCase));

        if (feature is null)
        {
            logger.LogWarning("AI tool '{ToolName}' failed: feature '{FeatureId}' not found.", Name, featureId);

            return $"Unable to find a feature with the ID: {featureId}.";
        }

        var isEnabled = await shellFeaturesManager.IsFeatureEnabledAsync(feature.Id);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return JsonSerializer.Serialize(feature.AsAIObject(isEnabled));
    }
}
