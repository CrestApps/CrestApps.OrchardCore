using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileTemplateChatModeDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly DefaultSpeechVoicePresenter _speechVoiceMenuService;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateChatModeDisplayDriver(
        IAIDeploymentManager deploymentManager,
        DefaultSpeechVoicePresenter speechVoiceMenuService,
        IStringLocalizer<AIProfileTemplateChatModeDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _speechVoiceMenuService = speechVoiceMenuService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileChatModeViewModel>("AIProfileChatMode_Edit", async model =>
        {
            if (template.Properties.ContainsKey(nameof(ChatModeProfileSettings)))
            {
                var settings = template.As<ChatModeProfileSettings>();
                model.ChatMode = settings.ChatMode;
                model.VoiceName = settings.VoiceName;
            }

            var (availableModes, hasConversation) = GetAvailableModes();
            model.AvailableModes = availableModes;
            model.AvailableVoices = hasConversation ? await GetAvailableVoicesAsync() : [];
        }).Location("Content:10%Interactions;3")
        .RenderWhen(async () =>
        {
            if (template.Source != AITemplateSources.Profile)
            {
                return false;
            }

            return await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText) != null;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new AIProfileChatModeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = template.As<ChatModeProfileSettings>();
        settings.ChatMode = model.ChatMode;
        settings.VoiceName = model.ChatMode == ChatMode.Conversation
            ? model.VoiceName?.Trim()
            : null;
        template.Put(settings);

        return Edit(template, context);
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
        => await _speechVoiceMenuService.GetVoiceMenuItemsAsync(deploymentName: null);
}
