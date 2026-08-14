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

internal sealed class AIProfileClaudeDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ClaudeClientService _claudeClientService;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileClaudeDisplayDriver"/> class.
    /// </summary>
    /// <param name="claudeClientService">The claude client service.</param>
    /// <param name="siteService">The site service.</param>
    public AIProfileClaudeDisplayDriver(
        ClaudeClientService claudeClientService,
        ISiteService siteService)
    {
        _claudeClientService = claudeClientService;
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditClaudeProfileViewModel>("AIProfileClaudeConfig_Edit", async model =>
        {
            var claudeSettings = profile.GetOrCreate<ClaudeSessionMetadata>();

            model.ClaudeModel = claudeSettings.ClaudeModel;
            model.ClaudeEffortLevel = claudeSettings.EffortLevel;

            var siteSettings = await _siteService.GetSettingsAsync<ClaudeSettings>();
            model.IsClaudeConfigured = siteSettings.IsConfigured();
            model.AvailableModels = await LoadModelsAsync(siteSettings, model.ClaudeModel);
        }).Location("Content:3.6%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditClaudeProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.Equals(profile.OrchestratorName, ClaudeOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            profile.Put(new ClaudeSessionMetadata
            {
                ClaudeModel = model.ClaudeModel,
                EffortLevel = model.ClaudeEffortLevel,
            });
        }

        return Edit(profile, context);
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
