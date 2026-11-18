using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.ChatSessions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ChatInteractions;

/// <summary>
/// Deletes chat interactions, as the Delete action and bulk Delete of the Chat Interactions page do, with the
/// interaction as the resource of the permission check. Interactions the caller may not delete, Profile Builder
/// sessions and the interaction the assistant is answering in are skipped and reported.
/// </summary>
public sealed class DeleteChatInteractionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteChatInteractions";

    private const int MaxInteractions = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "interactionIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the chat interactions to delete, from listChatInteractions."
        }
      },
      "required": ["interactionIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes chat interactions by id, with their messages and uploaded documents. It cannot be undone. Interactions you may not delete, and the one you are chatting in now, are skipped and reported.";

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

        // Deleting any interaction implies deleting one's own, so this is the least a caller needs; each interaction
        // is then checked on its own, as the page does.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.DeleteOwnChatInteraction))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "interactionIds", out var interactionIds) || interactionIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the chat interactions to delete in interactionIds.";
        }

        if (interactionIds.Count > MaxInteractions)
        {
            return $"Nothing was deleted: at most {MaxInteractions} chat interactions can be deleted at once.";
        }

        var manager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
        var runningInteractionId = RunningChat.GetInteractionId();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var interactionId in interactionIds)
        {
            var interaction = await manager.FindByIdAsync(interactionId, cancellationToken);

            if (interaction is null)
            {
                skipped.Add(new { id = interactionId, reason = "No chat interaction has this ID." });

                continue;
            }

            if (ChatInteractionToolSupport.IsProfileBuilderSession(interaction))
            {
                skipped.Add(new { id = interaction.ItemId, title = interaction.Title, reason = "This is a Profile Builder session; delete it in the Profile Builder." });

                continue;
            }

            if (string.Equals(interaction.ItemId, runningInteractionId, StringComparison.Ordinal))
            {
                skipped.Add(new { id = interaction.ItemId, title = interaction.Title, reason = "This is the chat interaction you are chatting in now." });

                continue;
            }

            if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.DeleteChatInteraction, interaction))
            {
                skipped.Add(new { id = interaction.ItemId, title = interaction.Title, reason = "You are not allowed to delete this chat interaction." });

                continue;
            }

            if (!await manager.DeleteAsync(interaction, cancellationToken))
            {
                skipped.Add(new { id = interaction.ItemId, title = interaction.Title, reason = "The chat interaction manager did not delete it." });

                continue;
            }

            deleted.Add(new { id = interaction.ItemId, title = interaction.Title, owner = interaction.Author });
        }

        return AgentToolResults.Json(new { deleted, skipped });
    }
}
