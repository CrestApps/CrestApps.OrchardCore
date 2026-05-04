using CrestApps.Core;
using CrestApps.Core.AI.Claude.Models;
using CrestApps.Core.AI.Claude.Services;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Claude.Services;
using CrestApps.OrchardCore.AI.Chat.Claude.Settings;
using CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Claude.Drivers;

internal sealed class ChatInteractionClaudeDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly ClaudeClientService _claudeClientService;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionClaudeDisplayDriver"/> class.
    /// </summary>
    /// <param name="claudeClientService">The claude client service.</param>
    /// <param name="siteService">The site service.</param>
    public ChatInteractionClaudeDisplayDriver(
        ClaudeClientService claudeClientService,
        ISiteService siteService)
    {
        _claudeClientService = claudeClientService;
        _siteService = siteService;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<EditClaudeProfileViewModel>("ChatInteractionClaudeConfig_Edit", async model =>
        {
            var claudeSettings = interaction.GetOrCreate<ClaudeSessionMetadata>();

            model.ClaudeModel = claudeSettings.ClaudeModel;
            model.ClaudeEffortLevel = claudeSettings.EffortLevel;

            var siteSettings = await _siteService.GetSettingsAsync<ClaudeSettings>();
            model.IsClaudeConfigured = siteSettings.IsConfigured();
            model.AvailableModels = await LoadModelsAsync(siteSettings, model.ClaudeModel);
        }).Location("Parameters:5#Settings;1");
    }

    private async Task<IList<SelectListItem>> LoadModelsAsync(ClaudeSettings siteSettings, string selectedModel)
    {
        if (siteSettings.AuthenticationType != ClaudeAuthenticationType.ApiKey ||
            string.IsNullOrWhiteSpace(siteSettings.ProtectedApiKey))
        {
            return ClaudeModelSelectListFactory.Build([], selectedModel, siteSettings.DefaultModel);
        }

        var models = await _claudeClientService.ListModelsAsync();
        return ClaudeModelSelectListFactory.Build(models, selectedModel, siteSettings.DefaultModel);
    }
}
