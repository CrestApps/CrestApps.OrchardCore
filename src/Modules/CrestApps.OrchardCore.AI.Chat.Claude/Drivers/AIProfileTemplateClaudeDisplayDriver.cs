using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Claude.Models;
using CrestApps.Core.AI.Claude.Services;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Claude.Services;
using CrestApps.OrchardCore.AI.Chat.Claude.Settings;
using CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Claude.Drivers;

internal sealed class AIProfileTemplateClaudeDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ClaudeClientService _claudeClientService;
    private readonly ISiteService _siteService;

    public AIProfileTemplateClaudeDisplayDriver(
        ClaudeClientService claudeClientService,
        ISiteService siteService)
    {
        _claudeClientService = claudeClientService;
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditClaudeProfileViewModel>("AIProfileTemplateClaudeConfig_Edit", async model =>
        {
            var claudeSettings = template.GetOrCreate<ClaudeSessionMetadata>();

            model.ClaudeModel = claudeSettings.ClaudeModel;
            model.ClaudeEffortLevel = claudeSettings.EffortLevel;

            var siteSettings = await _siteService.GetSettingsAsync<ClaudeSettings>();
            model.IsClaudeConfigured = siteSettings.IsConfigured();
            model.AvailableModels = await LoadModelsAsync(siteSettings, model.ClaudeModel);
        }).Location("Content:3%Parameters;5")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditClaudeProfileViewModel();
        var connectionModel = new AIProfileTemplateConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await context.Updater.TryUpdateModelAsync(connectionModel, Prefix);

        if (string.Equals(connectionModel.OrchestratorName, ClaudeOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            template.Put(new ClaudeSessionMetadata
            {
                ClaudeModel = model.ClaudeModel,
                EffortLevel = model.ClaudeEffortLevel,
            });
        }
        else
        {
            template.Remove<ClaudeSessionMetadata>();
        }

        return Edit(template, context);
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
