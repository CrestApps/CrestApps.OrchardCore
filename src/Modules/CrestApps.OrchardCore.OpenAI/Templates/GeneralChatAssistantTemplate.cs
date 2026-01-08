using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Templates;

/// <summary>
/// A template for creating a general-purpose chat assistant AI profile.
/// </summary>
public sealed class GeneralChatAssistantTemplate : IAIProfileTemplate
{
    private readonly IStringLocalizer<GeneralChatAssistantTemplate> _localizer;

    public GeneralChatAssistantTemplate(IStringLocalizer<GeneralChatAssistantTemplate> localizer)
    {
        _localizer = localizer;
    }

    public string Name => "GeneralChatAssistant";

    public LocalizedString DisplayName => _localizer["General Chat Assistant"];

    public LocalizedString Description => _localizer["A balanced template for general-purpose conversational AI."];

    public string ProfileSource => null; // Compatible with all sources

    public Task ApplyAsync(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Set profile type to Chat for conversational use
        profile.Type = AIProfileType.Chat;
        profile.TitleType = AISessionTitleType.Generated;
        profile.WelcomeMessage = "Hello! How can I assist you today?";

        // Set system message for a helpful assistant
        var metadata = profile.As<AIProfileMetadata>();
        metadata.SystemMessage = "You are a helpful AI assistant. Provide clear, accurate, and concise responses to user queries. Be friendly and professional in your interactions.";

        // Balanced parameters for general chat
        metadata.Temperature = 0.7f; // Moderate creativity
        metadata.MaxTokens = 2000; // Reasonable response length
        metadata.TopP = 1.0f;
        metadata.FrequencyPenalty = 0.0f;
        metadata.PresencePenalty = 0.0f;
        metadata.PastMessagesCount = 10; // Keep context of recent messages

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
