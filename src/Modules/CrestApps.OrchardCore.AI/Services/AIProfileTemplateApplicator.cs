using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Services;

internal static class AIProfileTemplateApplicator
{
    internal static void Apply(AIProfile profile, AIProfileTemplate template)
    {
        if (template.Properties != null)
        {
            foreach (var property in template.Properties)
            {
                // These describe the template itself rather than the profile built from it. Scenario metadata in
                // particular would otherwise ride along onto every profile created from a featured scenario.
                if (string.Equals(property.Key, nameof(ProfileTemplateMetadata), StringComparison.Ordinal) ||
                    string.Equals(property.Key, nameof(SystemPromptTemplateMetadata), StringComparison.Ordinal) ||
                    string.Equals(property.Key, nameof(ProfileScenarioMetadata), StringComparison.Ordinal))
                {
                    continue;
                }

                profile.Properties[property.Key] = property.Value;
                profile.Settings[property.Key] = JsonSerializer.SerializeToNode(property.Value);
            }
        }

        if (!string.IsNullOrEmpty(template.DisplayText))
        {
            profile.DisplayText = template.DisplayText;
        }

        if (!string.IsNullOrEmpty(template.Name))
        {
            profile.Name = template.Name;
        }

        var templateMetadata = template.GetOrCreate<ProfileTemplateMetadata>();

        if (templateMetadata.ProfileType.HasValue)
        {
            profile.Type = templateMetadata.ProfileType.Value;
        }

        if (!string.IsNullOrEmpty(templateMetadata.ChatDeploymentName))
        {
            profile.ChatDeploymentName = templateMetadata.ChatDeploymentName;
        }

        if (!string.IsNullOrEmpty(templateMetadata.UtilityDeploymentName))
        {
            profile.UtilityDeploymentName = templateMetadata.UtilityDeploymentName;
        }

        if (!string.IsNullOrEmpty(templateMetadata.OrchestratorName))
        {
            profile.OrchestratorName = templateMetadata.OrchestratorName;
        }

        // The chat deployment is the text model the profile talks to; the conversation deployment names the
        // model that carries a spoken conversation. A template written before that split named its
        // speech-to-speech model in a separate field, which is the conversation deployment now.
#pragma warning disable CS0618 // Type or member is obsolete - an existing template still applies.
        var conversationDeploymentName = !string.IsNullOrEmpty(templateMetadata.ConversationDeploymentName)
            ? templateMetadata.ConversationDeploymentName
            : templateMetadata.RealtimeDeploymentName;
#pragma warning restore CS0618

        if (!string.IsNullOrEmpty(conversationDeploymentName))
        {
            // A template edited through the chat mode editor carries its own ChatModeProfileSettings, copied
            // onto the profile above, and the mode it holds is the author's choice.
            var templateChoseChatMode = template.Properties?.ContainsKey(nameof(ChatModeProfileSettings)) == true;

            profile.AlterSettings<ChatModeProfileSettings>(chatModeSettings =>
            {
                chatModeSettings.ConversationDeploymentName = conversationDeploymentName;

                // A template that names a model to speak with is asking for a spoken conversation. Without
                // this the profile would carry the deployment and never use it.
                if (!templateChoseChatMode)
                {
                    chatModeSettings.ChatMode = ChatMode.Conversation;
                }
            });
        }

        if (templateMetadata.TitleType.HasValue)
        {
            profile.TitleType = templateMetadata.TitleType;
        }

        if (!string.IsNullOrEmpty(templateMetadata.WelcomeMessage))
        {
            profile.WelcomeMessage = templateMetadata.WelcomeMessage;
        }

        if (!string.IsNullOrEmpty(templateMetadata.PromptSubject))
        {
            profile.PromptSubject = templateMetadata.PromptSubject;
        }

        if (!string.IsNullOrEmpty(templateMetadata.PromptTemplate))
        {
            profile.PromptTemplate = templateMetadata.PromptTemplate;
        }

        var metadata = profile.GetOrCreate<AIProfileMetadata>();

        if (!string.IsNullOrEmpty(templateMetadata.SystemMessage))
        {
            metadata.SystemMessage = templateMetadata.SystemMessage;
        }

        if (templateMetadata.Temperature.HasValue)
        {
            metadata.Temperature = templateMetadata.Temperature;
        }

        if (templateMetadata.TopP.HasValue)
        {
            metadata.TopP = templateMetadata.TopP;
        }

        if (templateMetadata.FrequencyPenalty.HasValue)
        {
            metadata.FrequencyPenalty = templateMetadata.FrequencyPenalty;
        }

        if (templateMetadata.PresencePenalty.HasValue)
        {
            metadata.PresencePenalty = templateMetadata.PresencePenalty;
        }

        if (templateMetadata.MaxOutputTokens.HasValue)
        {
            metadata.MaxTokens = templateMetadata.MaxOutputTokens;
        }

        if (templateMetadata.PastMessagesCount.HasValue)
        {
            metadata.PastMessagesCount = templateMetadata.PastMessagesCount;
        }

        profile.Put(metadata);

        if (templateMetadata.ToolNames is { Length: > 0 })
        {
            var toolMetadata = profile.GetOrCreate<FunctionInvocationMetadata>();
            toolMetadata.Names = [.. templateMetadata.ToolNames];
            profile.Put(toolMetadata);
        }

        if (templateMetadata.AgentNames is { Length: > 0 })
        {
            var agentMetadata = profile.GetOrCreate<AgentInvocationMetadata>();
            agentMetadata.Names = [.. templateMetadata.AgentNames];
            profile.Put(agentMetadata);
        }

        if (!string.IsNullOrEmpty(templateMetadata.Description))
        {
            profile.Description = templateMetadata.Description;
        }

        if (templateMetadata.AgentAvailability.HasValue)
        {
            var agentMetadata = profile.GetOrCreate<AgentMetadata>();
            agentMetadata.Availability = templateMetadata.AgentAvailability.Value;
            profile.Put(agentMetadata);
        }
    }
}
