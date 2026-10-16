using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Copies what a builder session tuned to an AI profile, and an AI profile's settings into a builder session.
/// </summary>
/// <remarks>
/// The MCP and agent-to-agent connections are written under their metadata names rather than through their types,
/// so this module does not need to depend on those features to carry them across.
/// </remarks>
internal static class ProfileBuilderProfileMapper
{
    internal const string McpMetadataName = "AIProfileMcpMetadata";
    internal const string A2AMetadataName = "AIProfileA2AMetadata";

    /// <summary>
    /// Copies the session's settings, deployments and capabilities onto <paramref name="profile"/>. The profile's
    /// name, title and type are left to the caller.
    /// </summary>
    /// <param name="interaction">The session's chat interaction.</param>
    /// <param name="session">The builder session.</param>
    /// <param name="profile">The profile to write to.</param>
    public static void CopyToProfile(ChatInteraction interaction, ProfileBuilderSession session, AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);

        profile.ChatDeploymentName = NullIfEmpty(interaction.ChatDeploymentName);
        profile.UtilityDeploymentName = NullIfEmpty(interaction.UtilityDeploymentName);
        profile.OrchestratorName = NullIfEmpty(interaction.OrchestratorName);

        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        metadata.SystemMessage = interaction.SystemMessage;
        metadata.InitialPrompt = NullIfEmpty(session.InitialPrompt?.Trim());
        metadata.Temperature = interaction.Temperature;
        metadata.TopP = interaction.TopP;
        metadata.FrequencyPenalty = interaction.FrequencyPenalty;
        metadata.PresencePenalty = interaction.PresencePenalty;
        metadata.MaxTokens = interaction.MaxTokens;
        metadata.PastMessagesCount = interaction.PastMessagesCount;
        profile.Put(metadata);

        profile.Alter<FunctionInvocationMetadata>(tools => tools.Names = [.. interaction.ToolNames ?? []]);
        profile.Alter<AgentInvocationMetadata>(agents => agents.Names = [.. interaction.AgentNames ?? []]);

        WriteConnections(profile, McpMetadataName, interaction.McpConnectionIds);
        WriteConnections(profile, A2AMetadataName, interaction.A2AConnectionIds);

        CopyMetadata<DataSourceMetadata>(interaction, profile);
        CopyMetadata<AIDataSourceRagMetadata>(interaction, profile);
        CopyMetadata<AIToolInstanceMetadata>(interaction, profile);
        CopyMetadata<AIDeploymentParametersMetadata>(interaction, profile);
        CopyMetadata<PromptTemplateMetadata>(interaction, profile);

        // A conversation deployment on the interaction is what made role-play spoken, so the profile is set to
        // hold the same kind of conversation. Without one, the profile's own chat mode choice is left alone.
        if (!string.IsNullOrWhiteSpace(interaction.ConversationDeploymentName))
        {
            profile.AlterSettings<ChatModeProfileSettings>(settings =>
            {
                settings.ChatMode = ChatMode.Conversation;
                settings.ConversationDeploymentName = interaction.ConversationDeploymentName;

                if (!string.IsNullOrWhiteSpace(interaction.RealtimeVoiceName))
                {
                    settings.VoiceName = interaction.RealtimeVoiceName;
                }
            });
        }
    }

    /// <summary>
    /// Copies an existing profile's settings, deployments and capabilities into a session, so the profile can be
    /// improved by role-play.
    /// </summary>
    /// <param name="profile">The profile to read from.</param>
    /// <param name="interaction">The session's chat interaction.</param>
    /// <param name="session">The builder session.</param>
    public static void CopyFromProfile(AIProfile profile, ChatInteraction interaction, ProfileBuilderSession session)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(session);

        interaction.ChatDeploymentName = profile.ChatDeploymentName;
        interaction.UtilityDeploymentName = profile.UtilityDeploymentName;
        interaction.OrchestratorName = profile.OrchestratorName;

        if (profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            interaction.SystemMessage = metadata.SystemMessage;
            interaction.Temperature = metadata.Temperature;
            interaction.TopP = metadata.TopP;
            interaction.FrequencyPenalty = metadata.FrequencyPenalty;
            interaction.PresencePenalty = metadata.PresencePenalty;
            interaction.MaxTokens = metadata.MaxTokens;
            interaction.PastMessagesCount = metadata.PastMessagesCount;
            session.InitialPrompt = metadata.InitialPrompt;
        }

        if (profile.TryGet<FunctionInvocationMetadata>(out var tools) && tools.Names is not null)
        {
            interaction.ToolNames = [.. tools.Names];
        }

        if (profile.TryGet<AgentInvocationMetadata>(out var agents) && agents.Names is not null)
        {
            interaction.AgentNames = [.. agents.Names];
        }

        interaction.McpConnectionIds = ReadConnections(profile, McpMetadataName);
        interaction.A2AConnectionIds = ReadConnections(profile, A2AMetadataName);

        CopyMetadata<DataSourceMetadata>(profile, interaction);
        CopyMetadata<AIDataSourceRagMetadata>(profile, interaction);
        CopyMetadata<AIToolInstanceMetadata>(profile, interaction);
        CopyMetadata<AIDeploymentParametersMetadata>(profile, interaction);
        CopyMetadata<PromptTemplateMetadata>(profile, interaction);

        if (profile.TryGetSettings<ChatModeProfileSettings>(out var chatMode) &&
            chatMode.ChatMode == ChatMode.Conversation &&
            !string.IsNullOrWhiteSpace(chatMode.ConversationDeploymentName))
        {
            interaction.ConversationDeploymentName = chatMode.ConversationDeploymentName;
            interaction.RealtimeVoiceName = chatMode.VoiceName;
        }

        session.ProfileId = profile.ItemId;
    }

    private static void WriteConnections(ExtensibleEntity entity, string name, IList<string> connectionIds)
    {
        if (connectionIds is null || connectionIds.Count == 0)
        {
            if (entity.Properties.TryGetValue(name, out var existing) && existing is not null)
            {
                // Clear the connections but keep anything else the feature stores beside them.
                var node = JsonSerializer.SerializeToNode(existing, ExtensibleEntityExtensions.JsonSerializerOptions) as JsonObject ?? [];
                node["ConnectionIds"] = new JsonArray();
                entity.Put(name, node);
            }

            return;
        }

        var value = entity.Properties.TryGetValue(name, out var current) && current is not null
            ? JsonSerializer.SerializeToNode(current, ExtensibleEntityExtensions.JsonSerializerOptions) as JsonObject ?? []
            : [];

        value["ConnectionIds"] = new JsonArray([.. connectionIds.Select(id => JsonValue.Create(id))]);
        entity.Put(name, value);
    }

    private static List<string> ReadConnections(ExtensibleEntity entity, string name)
    {
        if (!entity.Properties.TryGetValue(name, out var value) || value is null)
        {
            return [];
        }

        var node = JsonSerializer.SerializeToNode(value, ExtensibleEntityExtensions.JsonSerializerOptions);

        if (node is not JsonObject json)
        {
            return [];
        }

        var ids = json.FirstOrDefault(property => string.Equals(property.Key, "ConnectionIds", StringComparison.OrdinalIgnoreCase)).Value as JsonArray;

        return ids?
            .Select(id => id?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList() ?? [];
    }

    private static void CopyMetadata<T>(ExtensibleEntity source, ExtensibleEntity target)
        where T : class, new()
    {
        if (!source.TryGet<T>(out var metadata))
        {
            target.Properties.Remove(typeof(T).Name);

            return;
        }

        // A copy rather than the same instance, so a later change to one never shows up in the other.
        var copy = JsonSerializer.SerializeToElement(metadata, ExtensibleEntityExtensions.JsonSerializerOptions)
            .Deserialize<T>(ExtensibleEntityExtensions.JsonSerializerOptions);

        target.Put(copy);
    }

    private static string NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
