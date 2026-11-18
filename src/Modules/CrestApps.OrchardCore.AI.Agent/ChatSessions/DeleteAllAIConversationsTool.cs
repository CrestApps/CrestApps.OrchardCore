using System.Text.Json;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ChatSessions;

/// <summary>
/// Deletes every one of the caller's conversations with one profile, as the Delete All action of the profile's chat
/// History page does, through the chat session manager. Like that page, it only deletes the caller's own
/// conversations. It is refused while the caller is chatting with that profile, since the running conversation would
/// go too.
/// </summary>
public sealed class DeleteAllAIConversationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAllAIConversations";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profile": {
          "type": "string",
          "description": "The ID or technical name of the AI profile whose conversations are deleted, from listAIProfiles."
        }
      },
      "required": ["profile"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes every one of your own conversations with one profile, as the profile's chat History page does, and returns how many were removed. It cannot be undone. It is refused for the profile you are chatting with now.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.DeleteAllChatSessions))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("profile", out var reference) || string.IsNullOrWhiteSpace(reference))
        {
            return "Nothing was deleted: give the ID or technical name of the AI profile in profile.";
        }

        reference = reference.Trim();

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = await profileManager.FindByIdAsync(reference, cancellationToken)
            ?? await profileManager.FindByNameAsync(reference, cancellationToken);

        if (profile is null)
        {
            return $"Nothing was deleted: no AI profile has the ID or name '{reference}'. Use listAIProfiles to find it.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.QueryAnyAIProfile, profile))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (string.Equals(profile.ItemId, RunningChat.GetSessionProfileId(), StringComparison.Ordinal))
        {
            return $"Nothing was deleted: you are chatting with the profile '{AIConfigurationGuard.GetTitle(profile)}' now, and deleting all its conversations would delete this one too. Delete them on the profile's History page, or from a chat with another profile.";
        }

        var count = await services.GetRequiredService<IAIChatSessionManager>().DeleteAllAsync(profile.ItemId, cancellationToken);

        return AgentToolResults.Json(new
        {
            profileId = profile.ItemId,
            profile = AIConfigurationGuard.GetTitle(profile),
            deleted = count,
            note = count == 0
                ? "You had no conversations with this profile, so nothing was deleted."
                : "Only your own conversations with this profile were deleted, as on the profile's History page.",
        });
    }
}
