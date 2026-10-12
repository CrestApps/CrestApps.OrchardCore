using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Copies the settings of a chat interaction to a new interaction, for the "Chat with Preset Settings" action.
/// </summary>
/// <remarks>
/// A clone starts with the same model, instructions, files, tools and knowledge as its source. The settings
/// that other features keep in <see cref="ExtensibleEntity.Properties"/> are copied one known type at a time,
/// not as the whole bag, because the bag can also hold data that belongs to one user only, for example the
/// access tokens of a Copilot session.
/// </remarks>
internal static class ChatInteractionCloner
{
    /// <summary>
    /// Copies the settings of <paramref name="source"/> to <paramref name="target"/>. The title, the identity,
    /// the owner and the chat history are not copied.
    /// </summary>
    /// <param name="source">The interaction to copy from.</param>
    /// <param name="target">The new interaction to copy to.</param>
    public static void CopySettings(ChatInteraction source, ChatInteraction target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.ChatDeploymentName = source.ChatDeploymentName;
        target.ConnectionName = source.ConnectionName;
        target.SystemMessage = source.SystemMessage;
        target.Temperature = source.Temperature;
        target.TopP = source.TopP;
        target.FrequencyPenalty = source.FrequencyPenalty;
        target.PresencePenalty = source.PresencePenalty;
        target.MaxTokens = source.MaxTokens;
        target.PastMessagesCount = source.PastMessagesCount;
        target.ToolNames = source.ToolNames.ToList();
        target.McpConnectionIds = source.McpConnectionIds.ToList();
        target.Documents = source.Documents.ToList();
        target.DocumentIndex = source.Documents.Count;

        CopyMetadata<DataSourceMetadata>(source, target);
        CopyMetadata<AIDataSourceRagMetadata>(source, target);
        CopyMetadata<AIToolInstanceMetadata>(source, target);
        CopyMetadata<AIDeploymentParametersMetadata>(source, target);
        CopyMetadata<PromptTemplateMetadata>(source, target);
    }

    private static void CopyMetadata<T>(ChatInteraction source, ChatInteraction target)
        where T : class, new()
    {
        if (!source.TryGet<T>(out var metadata))
        {
            return;
        }

        // A copy rather than the same instance, so a later change to one interaction never shows up in the other.
        var copy = JsonSerializer.SerializeToElement(metadata, ExtensibleEntityExtensions.JsonSerializerOptions)
            .Deserialize<T>(ExtensibleEntityExtensions.JsonSerializerOptions);

        target.Put(copy);
    }
}
