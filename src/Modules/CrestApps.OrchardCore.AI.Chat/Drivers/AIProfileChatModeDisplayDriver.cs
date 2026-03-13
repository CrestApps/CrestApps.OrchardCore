using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileChatModeDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly IStringLocalizer S;
    private readonly ILogger _logger;

    public AIProfileChatModeDisplayDriver(
        ISiteService siteService,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IStringLocalizer<AIProfileChatModeDisplayDriver> stringLocalizer,
        ILogger<AIProfileChatModeDisplayDriver> logger)
    {
        _siteService = siteService;
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        S = stringLocalizer;
        _logger = logger;
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

            var (availableModes, hasConversation) = await GetAvailableModesAsync();
            model.AvailableModes = availableModes;
            model.AvailableVoices = hasConversation ? await GetAvailableVoicesAsync() : [];
        }).Location("Content:5.3")
        .RenderWhen(async () =>
        {
            if (profile.Type != AIProfileType.Chat)
            {
                return false;
            }

            var site = await _siteService.GetSiteSettingsAsync();
            var deploymentSettings = site.As<DefaultAIDeploymentSettings>();

            return !string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId);
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

    private async Task<(IEnumerable<SelectListItem> Items, bool HasConversation)> GetAvailableModesAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var deploymentSettings = site.As<DefaultAIDeploymentSettings>();

        var hasSTT = !string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId);
        var hasTTS = !string.IsNullOrEmpty(deploymentSettings.DefaultTextToSpeechDeploymentId);

        var modes = new List<SelectListItem>
        {
            new(S["Text Only"], nameof(ChatMode.TextInput)),
        };

        if (hasSTT)
        {
            modes.Add(new SelectListItem(S["Audio Input"], nameof(ChatMode.AudioInput)));
        }

        var hasConversation = hasSTT && hasTTS;

        if (hasConversation)
        {
            modes.Add(new SelectListItem(S["Conversation"], nameof(ChatMode.Conversation)));
        }

        return (modes, hasConversation);
    }

    private async Task<IEnumerable<SelectListItem>> GetAvailableVoicesAsync()
    {
        var voices = new List<SelectListItem>();

        try
        {
            var site = await _siteService.GetSiteSettingsAsync();
            var deploymentSettings = site.As<DefaultAIDeploymentSettings>();

            if (!string.IsNullOrEmpty(deploymentSettings.DefaultTextToSpeechDeploymentId))
            {
                var deployment = await _deploymentManager.FindByIdAsync(deploymentSettings.DefaultTextToSpeechDeploymentId);

                if (deployment != null)
                {
                    using var client = await _clientFactory.CreateTextToSpeechClientAsync(deployment);
                    var speechVoices = await client.GetVoicesAsync();

                    foreach (var voiceGroup in speechVoices
                        .OrderBy(v => v.Language)
                        .ThenBy(v => v.Name)
                        .GroupBy(v => v.Language ?? S["Unknown"]))
                    {
                        var group = new SelectListGroup { Name = voiceGroup.Key };

                        foreach (var voice in voiceGroup)
                        {
                            voices.Add(new SelectListItem(voice.Name, voice.Id) { Group = group });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch available TTS voices.");
        }

        return voices;
    }
}
