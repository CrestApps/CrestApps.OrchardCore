using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Deletes one AI profile, as the Delete action of the profiles list does. It refuses a profile marked not removable,
/// the assistant that is running with its agents, a profile people or clients can still reach (a published chat
/// widget, the admin chat widget, a browser API key, the Anonymous role, or every profile as an always-available
/// agent), and a profile another profile calls as an agent.
/// </summary>
public sealed class DeleteAIProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileId": {
          "type": "string",
          "description": "The ID of the one profile to delete, from listAIProfiles."
        }
      },
      "required": ["profileId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one profile by id; a deleted profile cannot be restored. Refused for a profile marked not removable, the running assistant and its agents, a profile people or clients can reach (chat widget, admin chat, API key, visitors) and a profile another profile uses as an agent: remove those uses first.";

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

        if (!arguments.TryGetFirstString("profileId", out var profileId) || string.IsNullOrWhiteSpace(profileId))
        {
            return "Nothing was deleted: give the ID of the one profile to delete in profileId.";
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = await profileManager.FindByIdAsync(profileId.Trim(), cancellationToken);

        if (profile is null)
        {
            return $"Nothing was deleted: no AI profile has the ID '{profileId.Trim()}'. Use listAIProfiles to find the ID.";
        }

        var title = AIConfigurationGuard.GetTitle(profile);
        var editPath = AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId);
        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);

        if (protectedConfiguration.IsProtected(profile))
        {
            return AIProfileToolSupport.RunningAssistantRefusal($"the profile '{title}'", editPath);
        }

        if (!profile.GetSettings<AIProfileSettings>().IsRemovable)
        {
            return $"Nothing was deleted: the profile '{title}' is marked not removable.";
        }

        var uses = await GetUsesAsync(services, profileManager, profile, cancellationToken);

        if (uses.Count > 0)
        {
            return $"Nothing was deleted: the profile '{title}' is still in use: {string.Join("; ", uses)}. Remove those uses first, or delete the profile on its admin page if you are sure: {editPath}";
        }

        if (!await profileManager.DeleteAsync(profile, cancellationToken))
        {
            return $"Nothing was deleted: the profile manager did not delete the profile '{title}'.";
        }

        return AgentToolResults.Json(new
        {
            deleted = true,
            id = profile.ItemId,
            name = profile.Name,
            title,
        });
    }

    /// <summary>
    /// Gets the ways a profile is still reached or used: by people or clients who chat with it, and by other profiles
    /// that call it as an agent.
    /// </summary>
    private static async Task<List<string>> GetUsesAsync(IServiceProvider services, IAIProfileManager profileManager, AIProfile profile, CancellationToken cancellationToken)
    {
        var uses = await AIConfigurationGuard.GetUntrustedReachAsync(services, profile, cancellationToken);

        if (string.Equals(await AIConfigurationGuard.GetAdminWidgetProfileIdAsync(services), profile.ItemId, StringComparison.Ordinal))
        {
            uses.Add("it is the profile of the admin chat widget");
        }

        if (!string.IsNullOrEmpty(profile.Name))
        {
            var callers = (await profileManager.GetAllAsync(cancellationToken))
                .Where(other => !string.Equals(other.ItemId, profile.ItemId, StringComparison.Ordinal))
                .Where(other => AIProfileCapabilities.Read(other).AgentNames.Contains(profile.Name))
                .Select(AIConfigurationGuard.GetTitle)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (callers.Count > 0)
            {
                uses.Add($"{AIConfigurationGuard.Join(callers.Select(caller => $"'{caller}'").ToList())} {(callers.Count == 1 ? "uses" : "use")} it as an agent");
            }
        }

        return uses;
    }
}
