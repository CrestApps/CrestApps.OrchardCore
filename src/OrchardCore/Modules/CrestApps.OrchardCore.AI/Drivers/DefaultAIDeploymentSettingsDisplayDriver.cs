using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class DefaultAIDeploymentSettingsDisplayDriver : SiteDisplayDriver<DefaultAIDeploymentSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDeploymentManager _deploymentManager;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public DefaultAIDeploymentSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAIDeploymentManager deploymentManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _deploymentManager = deploymentManager;
    }

    public override IDisplayResult Edit(ISite site, DefaultAIDeploymentSettings settings, BuildEditorContext context)
    {
        return Initialize<DefaultAIDeploymentSettingsViewModel>("DefaultAIDeploymentSettings_Edit", async model =>
        {
            model.DefaultChatDeploymentId = settings.DefaultChatDeploymentId;
            model.DefaultUtilityDeploymentId = settings.DefaultUtilityDeploymentId;
            model.DefaultEmbeddingDeploymentId = settings.DefaultEmbeddingDeploymentId;
            model.DefaultImageDeploymentId = settings.DefaultImageDeploymentId;
            model.DefaultSpeechToTextDeploymentId = settings.DefaultSpeechToTextDeploymentId;
            model.DefaultTextToSpeechDeploymentId = settings.DefaultTextToSpeechDeploymentId;
            model.DefaultTextToSpeechVoiceId = settings.DefaultTextToSpeechVoiceId;

            model.ChatDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Chat));

            model.UtilityDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Utility));

            model.EmbeddingDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding));

            model.ImageDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Image));

            model.SpeechToTextDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.SpeechToText));

            model.TextToSpeechDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.TextToSpeech));
        }).Location("Content:4%Default Deployments;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, DefaultAIDeploymentSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new DefaultAIDeploymentSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DefaultChatDeploymentId = model.DefaultChatDeploymentId;
        settings.DefaultUtilityDeploymentId = model.DefaultUtilityDeploymentId;
        settings.DefaultEmbeddingDeploymentId = model.DefaultEmbeddingDeploymentId;
        settings.DefaultImageDeploymentId = model.DefaultImageDeploymentId;
        settings.DefaultSpeechToTextDeploymentId = model.DefaultSpeechToTextDeploymentId;
        settings.DefaultTextToSpeechDeploymentId = model.DefaultTextToSpeechDeploymentId;
        settings.DefaultTextToSpeechVoiceId = model.DefaultTextToSpeechVoiceId?.Trim();

        return Edit(site, settings, context);
    }

    private static IEnumerable<SelectListItem> BuildGroupedDeploymentItems(IEnumerable<AIDeployment> deployments)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(d => d.ConnectionNameAlias ?? d.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                SelectListGroup group = null;

                var groupKey = d.ConnectionNameAlias ?? d.ConnectionName;

                if (!string.IsNullOrEmpty(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup { Name = groupKey };
                    groups[groupKey] = group;
                }

                return new SelectListItem(d.Name, d.ItemId) { Group = group };
            });
    }
}
