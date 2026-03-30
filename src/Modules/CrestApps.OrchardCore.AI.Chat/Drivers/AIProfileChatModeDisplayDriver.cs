using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileChatModeDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly DefaultSpeechVoicePresenter _speechVoiceMenuService;

    internal readonly IStringLocalizer S;

    public AIProfileChatModeDisplayDriver(
        IAIDeploymentManager deploymentManager,
        DefaultSpeechVoicePresenter speechVoiceMenuService,
        IStringLocalizer<AIProfileChatModeDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _speechVoiceMenuService = speechVoiceMenuService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileChatModeViewModel>("AIProfileChatMode_Edit", async model =>
        {
            if (profile.TryGetSettings<ChatModeProfileSettings>(out var settings))
            {
                model.ChatMode = settings.ChatMode;
                model.VoiceName = settings.VoiceName;
            }

            var (availableModes, hasConversation) = GetAvailableModes();
            model.AvailableModes = availableModes;
            model.AvailableVoices = hasConversation ? await GetAvailableVoicesAsync() : [];
        }).Location("Content:10%Interactions;3")
        .RenderWhen(async () =>
        {
            if (profile.Type != AIProfileType.Chat)
            {
                return false;
            }

            return await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText) != null;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (profile.Type != AIProfileType.Chat)
        {
            return null;
        }

        var model = new AIProfileChatModeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<ChatModeProfileSettings>(settings =>
        {
            settings.ChatMode = model.ChatMode;
            settings.VoiceName = model.ChatMode == ChatMode.Conversation
                ? model.VoiceName?.Trim()
                : null;
        });

        return Edit(profile, context);
    }

    private (IEnumerable<SelectListItem> Items, bool HasConversation) GetAvailableModes()
    {
        var modes = new List<SelectListItem>
        {
            new(S["Text only"], nameof(ChatMode.TextInput)),
            new(S["Audio input"], nameof(ChatMode.AudioInput)),
            new(S["Conversation"], nameof(ChatMode.Conversation)),
        };

        return (modes, true);
    }

    private async Task<IEnumerable<SelectListItem>> GetAvailableVoicesAsync()
        => await _speechVoiceMenuService.GetVoiceMenuItemsAsync(deploymentId: null);
}
