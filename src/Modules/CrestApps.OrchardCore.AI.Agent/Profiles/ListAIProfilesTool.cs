using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

public sealed class ListAIProfilesTool : AIFunction
{
    public const string TheName = "listAIProfiles";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "type": {
              "type": "string",
              "description": "Optional. Filter profiles by type: 'Chat', 'Utility', or 'Embedding'.",
              "enum": ["Chat", "Utility", "Embedding"]
            },
            "onlyWithMetricsEnabled": {
              "type": "boolean",
              "description": "Optional. When true, only return profiles that have session analytics metrics enabled."
            },
            "onlyWithDataExtraction": {
              "type": "boolean",
              "description": "Optional. When true, only return profiles that have data extraction enabled."
            },
            "onlyWithPostSessionProcessing": {
              "type": "boolean",
              "description": "Optional. When true, only return profiles that have post-session processing enabled."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description =>
        "Lists AI profiles with optional filters. " +
        "Can filter by profile type and by enabled features such as session analytics, data extraction, or post-session processing.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();

        var profiles = (await profileManager.GetAllAsync()).ToList();

        if (arguments.TryGetFirstString("type", out var typeStr)
            && Enum.TryParse<AIProfileType>(typeStr, ignoreCase: true, out var profileType))
        {
            profiles = profiles.Where(p => p.Type == profileType).ToList();
        }

        if (arguments.TryGetFirst<bool>("onlyWithMetricsEnabled", out var metricsEnabled) && metricsEnabled)
        {
            profiles = profiles.Where(p => p.As<AIProfileAnalyticsMetadata>().EnableSessionMetrics).ToList();
        }

        if (arguments.TryGetFirst<bool>("onlyWithDataExtraction", out var dataExtraction) && dataExtraction)
        {
            profiles = profiles.Where(p => p.GetSettings<AIProfileDataExtractionSettings>().EnableDataExtraction).ToList();
        }

        if (arguments.TryGetFirst<bool>("onlyWithPostSessionProcessing", out var postSession) && postSession)
        {
            profiles = profiles.Where(p => p.GetSettings<AIProfilePostSessionSettings>().EnablePostSessionProcessing).ToList();
        }

        var result = profiles.Select(p => new JsonObject
        {
            ["id"] = p.ItemId,
            ["name"] = p.Name,
            ["displayText"] = p.DisplayText,
            ["type"] = p.Type.ToString(),
            ["source"] = p.Source,
            ["metricsEnabled"] = p.As<AIProfileAnalyticsMetadata>().EnableSessionMetrics,
            ["dataExtractionEnabled"] = p.GetSettings<AIProfileDataExtractionSettings>().EnableDataExtraction,
            ["postSessionProcessingEnabled"] = p.GetSettings<AIProfilePostSessionSettings>().EnablePostSessionProcessing,
        }).ToList();

        return JsonSerializer.Serialize(new { profiles = result, count = result.Count });
    }
}
