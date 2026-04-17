using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;
using USR = OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;

internal sealed class AIProfileTemplateCopilotDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly GitHubOAuthService _oauthService;
    private readonly UserManager<USR.IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateCopilotDisplayDriver(
        GitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        IStringLocalizer<AIProfileTemplateCopilotDisplayDriver> stringLocalizer)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditCopilotProfileViewModel>("AIProfileTemplateCopilotConfig_Edit", async model =>
        {
            var copilotSettings = template.GetOrCreate<CopilotSessionMetadata>();

            model.CopilotModel = copilotSettings.CopilotModel;
            model.IsAllowAll = copilotSettings.IsAllowAll;
            model.CopilotReasoningEffort = copilotSettings.ReasoningEffort;

            var siteSettings = await _siteService.GetSettingsAsync<CopilotSettings>();
            model.AuthenticationType = siteSettings.AuthenticationType;
            model.IsCopilotConfigured = siteSettings.IsConfigured();

            if (!model.IsCopilotConfigured)
            {
                model.AvailableModels = [];

                return;
            }

            if (siteSettings.AuthenticationType == CopilotAuthenticationType.ApiKey)
            {
                model.AvailableModels = [];
            }
            else if (siteSettings.AuthenticationType == CopilotAuthenticationType.GitHubOAuth)
            {
                var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User);

                if (user is not null)
                {
                    var userId = await _userManager.GetUserIdAsync(user);
                    model.IsAuthenticated = await _oauthService.IsAuthenticatedAsync(userId);

                    if (model.IsAuthenticated)
                    {
                        var credential = await _oauthService.GetCredentialAsync(userId);
                        model.GitHubUsername = credential?.GitHubUsername;

                        var models = await _oauthService.ListModelsAsync(userId);

                        if (models.Count > 0)
                        {
                            model.AvailableModels = models
                                .Select(m => new SelectListItem(CopilotModelDisplayTextFormatter.Format(m), m.Id))
                                .ToList();
                        }
                    }
                }

                model.AvailableModels ??= [];
            }
            else
            {
                model.AvailableModels = [];
            }
        }).Location("Content:2%Parameters;5")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditCopilotProfileViewModel();
        var connectionModel = new AIProfileTemplateConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await context.Updater.TryUpdateModelAsync(connectionModel, Prefix);

        if (string.Equals(connectionModel.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            template.Put(new CopilotSessionMetadata
            {
                CopilotModel = model.CopilotModel,
                IsAllowAll = model.IsAllowAll,
                ReasoningEffort = model.CopilotReasoningEffort,
            });
        }
        else
        {
            template.Remove<CopilotSessionMetadata>();
        }

        return Edit(template, context);
    }
}
