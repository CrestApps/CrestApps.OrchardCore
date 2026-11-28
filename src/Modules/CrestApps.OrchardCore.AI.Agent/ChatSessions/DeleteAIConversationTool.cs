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
/// Deletes one of the caller's AI conversations and its transcript, as the Delete action of a profile's chat History
/// page does, through the chat session manager. Like that page, it only deletes the caller's own conversations, and it
/// never deletes the conversation it is answering in.
/// </summary>
public sealed class DeleteAIConversationTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIConversation";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "sessionId": {
          "type": "string",
          "description": "The session ID of the conversation to delete, from listAIConversations."
        }
      },
      "required": ["sessionId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one of your own AI conversations and its transcript by session id, as a profile's chat History page does. It cannot be undone. The conversation you are chatting in now cannot be deleted.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.DeleteChatSession))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("sessionId", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return "Nothing was deleted: give the session ID of the conversation in sessionId.";
        }

        sessionId = sessionId.Trim();

        if (string.Equals(sessionId, RunningChat.GetSessionId(), StringComparison.Ordinal))
        {
            return "Nothing was deleted: this is the conversation you are chatting in now. Start another chat to delete it, or delete it on the profile's History page.";
        }

        var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
        var session = await sessionManager.FindByIdAsync(sessionId, cancellationToken);

        if (session is null)
        {
            return $"Nothing was deleted: no AI conversation has the session ID '{sessionId}'. Use listAIConversations to find it.";
        }

        if (!string.Equals(session.UserId, RunningChat.GetCallerId(services), StringComparison.Ordinal))
        {
            return "Nothing was deleted: this is not one of your own conversations. Like a profile's chat History page, this tool only deletes the conversations of the person in the chat.";
        }

        var profile = string.IsNullOrEmpty(session.ProfileId) ? null : await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(session.ProfileId, cancellationToken);

        if (profile is null)
        {
            return "Nothing was deleted: the AI profile of this conversation no longer exists.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.QueryAnyAIProfile, profile))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!await sessionManager.DeleteAsync(session.SessionId, cancellationToken))
        {
            return "Nothing was deleted: the chat session manager could not delete the conversation. Delete it on the profile's History page.";
        }

        return AgentToolResults.Json(new
        {
            deleted = new
            {
                sessionId = session.SessionId,
                title = session.Title,
                profileId = profile.ItemId,
                profile = AIConfigurationGuard.GetTitle(profile),
            },
        });
    }
}
