using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for custom chat instances.
/// </summary>
public sealed class CustomChatInstanceDisplayDriver : DisplayDriver<AIChatSession>
{
    private readonly AIOptions _aiOptions;

    public CustomChatInstanceDisplayDriver(IOptions<AIOptions> aiOptions)
    {
        _aiOptions = aiOptions.Value;
    }

    public override Task<IDisplayResult> EditAsync(AIChatSession session, BuildEditorContext context)
    {
        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // For custom instances, we need to create a virtual profile representation for the chat UI
        var virtualProfile = new AIProfile
        {
            ItemId = session.ProfileId,
            Name = $"custom-{session.SessionId}",
            DisplayText = session.Title,
            Type = AIProfileType.Chat,
            ConnectionName = metadata.ConnectionName,
            DeploymentId = metadata.DeploymentId,
            TitleType = AISessionTitleType.InitialPrompt,
            Source = metadata.Source ?? GetDefaultSource()
        };

        // Add metadata
        virtualProfile.Put(new AIProfileMetadata
        {
            SystemMessage = metadata.SystemMessage,
            MaxTokens = metadata.MaxTokens,
            Temperature = metadata.Temperature,
            TopP = metadata.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty,
            PastMessagesCount = metadata.PastMessagesCount,
            UseCaching = metadata.UseCaching
        });

        // Add tool metadata if tools are selected
        if (metadata.ToolNames?.Length > 0)
        {
            virtualProfile.Put(new AIProfileFunctionInvocationMetadata
            {
                Names = metadata.ToolNames
            });
        }

        var headerResult = Initialize<ChatSessionCapsuleViewModel>("AIChatSessionHeader", model =>
        {
            model.Session = session;
            model.Profile = virtualProfile;
            model.IsNew = context.IsNew;
        }).Location("Header");

        var contentResult = Initialize<ChatSessionCapsuleViewModel>("AIChatSessionChat", model =>
        {
            model.Session = session;
            model.Profile = virtualProfile;
            model.IsNew = context.IsNew;
        }).Location("Content");

        return Task.FromResult<IDisplayResult>(Combine(headerResult, contentResult));
    }

    private string GetDefaultSource()
    {
        // Get the first available profile source
        return _aiOptions.ProfileSources.Keys.FirstOrDefault();
    }
}
