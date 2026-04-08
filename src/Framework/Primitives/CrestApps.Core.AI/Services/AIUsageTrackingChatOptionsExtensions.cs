using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.AI.Services;

internal static class AIUsageTrackingChatOptionsExtensions
{
    public static ChatOptions AddUsageTracking(
        this ChatOptions options,
        AICompletionContext completionContext = null,
        AIChatSession session = null,
        ChatInteraction interaction = null,
        string clientName = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (completionContext != null)
        {
            options.AdditionalProperties ??= [];
            options.AdditionalProperties[AICompletionContextKeys.CompletionContext] = completionContext;
        }

        if (session != null)
        {
            options.AdditionalProperties ??= [];
            options.AdditionalProperties[AICompletionContextKeys.Session] = session;
        }

        if (interaction != null)
        {
            options.AdditionalProperties ??= [];
            options.AdditionalProperties[AICompletionContextKeys.Interaction] = interaction;
            options.AdditionalProperties[AICompletionContextKeys.InteractionId] = interaction.ItemId;
        }

        if (!string.IsNullOrEmpty(clientName))
        {
            options.AdditionalProperties ??= [];
            options.AdditionalProperties[AICompletionContextKeys.ClientName] = clientName;
        }

        return options;
    }
}
