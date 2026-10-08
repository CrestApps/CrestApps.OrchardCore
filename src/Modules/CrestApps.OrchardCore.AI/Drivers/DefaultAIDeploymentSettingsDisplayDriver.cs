using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
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

/// <summary>
/// Display driver for managing the default AI deployment settings on the site settings page.
/// </summary>
public sealed class DefaultAIDeploymentSettingsDisplayDriver : SiteDisplayDriver<DefaultAIDeploymentSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDeploymentManager _deploymentManager;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIDeploymentSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
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
            model.DefaultChatDeploymentName = await NormalizeDeploymentSelectorAsync(settings.DefaultChatDeploymentName);
            model.DefaultUtilityDeploymentName = await NormalizeDeploymentSelectorAsync(settings.DefaultUtilityDeploymentName);
            model.DefaultEmbeddingDeploymentName = await NormalizeDeploymentSelectorAsync(settings.DefaultEmbeddingDeploymentName);
            model.DefaultImageDeploymentName = await NormalizeDeploymentSelectorAsync(settings.DefaultImageDeploymentName);
            model.DefaultVisionDeploymentName = await NormalizeDeploymentSelectorAsync(settings.DefaultVisionDeploymentName);
            model.DefaultSpeechToTextDeploymentName = await NormalizeDeploymentSelectorAsync(settings.DefaultSpeechToTextDeploymentName);
            model.DefaultTextToSpeechDeploymentName = await NormalizeDeploymentSelectorAsync(settings.DefaultTextToSpeechDeploymentName);
            model.DefaultTextToSpeechVoiceId = settings.DefaultTextToSpeechVoiceId;

            model.ChatDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Chat);
            model.UtilityDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Utility);
            model.EmbeddingDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Embedding);
            model.ImageDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Image);
            model.VisionDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Vision);
            model.SpeechToTextDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.SpeechToText);
            model.TextToSpeechDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.TextToSpeech);
        }).Location("Content:2%Default Deployments;1")
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

        settings.DefaultChatDeploymentName = model.DefaultChatDeploymentName;
        settings.DefaultUtilityDeploymentName = model.DefaultUtilityDeploymentName;
        settings.DefaultEmbeddingDeploymentName = model.DefaultEmbeddingDeploymentName;
        settings.DefaultImageDeploymentName = model.DefaultImageDeploymentName;
        settings.DefaultVisionDeploymentName = model.DefaultVisionDeploymentName;
        settings.DefaultSpeechToTextDeploymentName = model.DefaultSpeechToTextDeploymentName;
        settings.DefaultTextToSpeechDeploymentName = model.DefaultTextToSpeechDeploymentName;
        settings.DefaultTextToSpeechVoiceId = model.DefaultTextToSpeechVoiceId?.Trim();

        return Edit(site, settings, context);
    }

    private async Task<string> NormalizeDeploymentSelectorAsync(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return selector;
        }

        var deployment = await _deploymentManager.FindByIdAsync(selector);

        return deployment?.Name ?? selector;
    }
}
