using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

public sealed class ViewAIProfileTool : AIFunction
{
    public const string TheName = "viewAIProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "profileId": {
              "type": "string",
              "description": "The unique ID of the AI profile to view."
            },
            "profileName": {
              "type": "string",
              "description": "The technical name of the AI profile to view. Used if profileId is not provided."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description =>
        "Retrieves detailed information about a specific AI profile by its ID or technical name. " +
        "Returns the full profile configuration including type, connection, welcome message, " +
        "analytics settings, data extraction settings, and post-session processing settings.";

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

        AIProfile profile = null;

        if (arguments.TryGetFirstString("profileId", out var profileId) && !string.IsNullOrWhiteSpace(profileId))
        {
            profile = await profileManager.FindByIdAsync(profileId);
        }
        else if (arguments.TryGetFirstString("profileName", out var profileName) && !string.IsNullOrWhiteSpace(profileName))
        {
            profile = await profileManager.FindByNameAsync(profileName);
        }

        if (profile == null)
        {
            return JsonSerializer.Serialize(new { error = "AI profile not found." });
        }

        var analyticsMetadata = profile.As<AIProfileAnalyticsMetadata>();
        var dataExtractionSettings = profile.GetSettings<AIProfileDataExtractionSettings>();
        var postSessionSettings = profile.GetSettings<AIProfilePostSessionSettings>();

        var result = new JsonObject
        {
            ["id"] = profile.ItemId,
            ["name"] = profile.Name,
            ["displayText"] = profile.DisplayText,
            ["type"] = profile.Type.ToString(),
            ["source"] = profile.Source,
            ["connectionName"] = profile.ConnectionName,
            ["deploymentId"] = profile.DeploymentId,
            ["welcomeMessage"] = profile.WelcomeMessage,
            ["promptSubject"] = profile.PromptSubject,
            ["createdUtc"] = profile.CreatedUtc.ToString("o"),
            ["analytics"] = new JsonObject
            {
                ["enableSessionMetrics"] = analyticsMetadata.EnableSessionMetrics,
            },
            ["dataExtraction"] = new JsonObject
            {
                ["enabled"] = dataExtractionSettings.EnableDataExtraction,
                ["extractionCheckInterval"] = dataExtractionSettings.ExtractionCheckInterval,
                ["sessionInactivityTimeoutInMinutes"] = dataExtractionSettings.SessionInactivityTimeoutInMinutes,
                ["entries"] = JsonSerializer.SerializeToNode(dataExtractionSettings.DataExtractionEntries
                    .Select(e => new
                    {
                        name = e.Name,
                        description = e.Description,
                        allowMultipleValues = e.AllowMultipleValues,
                        isUpdatable = e.IsUpdatable,
                    })),
            },
            ["postSessionProcessing"] = new JsonObject
            {
                ["enabled"] = postSessionSettings.EnablePostSessionProcessing,
                ["tasks"] = JsonSerializer.SerializeToNode(postSessionSettings.PostSessionTasks
                    .Select(t => new
                    {
                        name = t.Name,
                        type = t.Type.ToString(),
                        instructions = t.Instructions,
                        allowMultipleValues = t.AllowMultipleValues,
                        options = t.Options.Select(o => new
                        {
                            value = o.Value,
                            description = o.Description,
                        }),
                    })),
            },
        };

        return JsonSerializer.Serialize(result);
    }
}
