using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Deletes AI profiles, as the Delete action and bulk Delete of the profiles list do. Profiles marked not removable and
/// the assistant that is running, with its agents, are skipped and reported.
/// </summary>
public sealed class DeleteAIProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIProfile";

    private const int MaxProfiles = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the profiles to delete, from listAIProfiles."
        }
      },
      "required": ["profileIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one or more profiles by id. Profiles marked not removable, the running assistant and its agents are skipped and reported. A deleted profile cannot be restored.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIProfiles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "profileIds", out var profileIds) || profileIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the profiles to delete in profileIds.";
        }

        if (profileIds.Count > MaxProfiles)
        {
            return $"Nothing was deleted: at most {MaxProfiles} profiles can be deleted at once.";
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var profileId in profileIds)
        {
            var profile = await profileManager.FindByIdAsync(profileId, cancellationToken);

            if (profile is null)
            {
                skipped.Add(new { id = profileId, reason = "No profile has this ID." });

                continue;
            }

            var title = AIConfigurationGuard.GetTitle(profile);

            if (protectedConfiguration.IsProtected(profile))
            {
                skipped.Add(new { id = profile.ItemId, title, reason = AIProfileToolSupport.RunningAssistantRefusal($"the profile '{title}'", AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId)) });

                continue;
            }

            if (!profile.GetSettings<AIProfileSettings>().IsRemovable)
            {
                skipped.Add(new { id = profile.ItemId, title, reason = "The profile is marked not removable." });

                continue;
            }

            if (await profileManager.DeleteAsync(profile, cancellationToken))
            {
                deleted.Add(new { id = profile.ItemId, name = profile.Name, title });
            }
            else
            {
                skipped.Add(new { id = profile.ItemId, title, reason = "The profile manager did not delete it." });
            }
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
