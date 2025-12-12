using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class CustomChatInstanceDisplayDriver : DisplayDriver<AIChatSession>
{
    private readonly AIOptions _aiOptions;

    public CustomChatInstanceDisplayDriver(IOptions<AIOptions> aiOptions)
    {
        _aiOptions = aiOptions.Value;
    }

    public override IDisplayResult Display(AIChatSession session, BuildDisplayContext context)
    {
        var result = Initialize<DisplayAIChatSessionViewModel>("AIChatSessionListItem_AdminChatSession", model =>
        {
            model.Session = session;
        }).Location(AIConstants.ShapeLocations.SummaryAdmin, "Content")
        .OnGroup(AIConstants.DisplayGroups.AdminChatSession);

        return result;
    }

    public override Task<IDisplayResult> EditAsync(AIChatSession session, BuildEditorContext context)
    {
        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        var virtualProfile = new AIProfile
        {
            ItemId = session.ProfileId,
            Name = $"custom-{session.SessionId}",
            DisplayText = session.Title,
            Type = AIProfileType.Chat,
            ConnectionName = metadata.ConnectionName,
            DeploymentId = metadata.DeploymentId,
            TitleType = AISessionTitleType.Generated,
            Source = metadata.Source ?? GetDefaultSource()
        };

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

        if (metadata.ToolNames?.Length > 0)
        {
            virtualProfile.Put(new AIProfileFunctionInvocationMetadata
            {
                Names = metadata.ToolNames
            });
        }

        var headerResult = Initialize<ChatSessionCapsuleViewModel>("AIChatSessionHeader_AdminChatSession", model =>
        {
            model.Session = session;
            model.Profile = virtualProfile;
            model.IsNew = context.IsNew;
        }).Location("Header").OnGroup(AIConstants.DisplayGroups.AdminChatSession);

        var contentResult = Initialize<ChatSessionCapsuleViewModel>("AIChatSessionChat_AdminChatSession", model =>
        {
            model.Session = session;
            model.Profile = virtualProfile;
            model.IsNew = context.IsNew;
        }).Location("Content").OnGroup(AIConstants.DisplayGroups.AdminChatSession);

        return Task.FromResult<IDisplayResult>(Combine(headerResult, contentResult));
    }

    private string GetDefaultSource()
    {
        return _aiOptions.ProfileSources.Keys.FirstOrDefault();
    }
}
