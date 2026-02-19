using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using USR = OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;

internal sealed class AIProfileCopilotDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly GitHubOAuthService _oauthService;
    private readonly UserManager<USR.IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AIProfileCopilotDisplayDriver(
        GitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIProfileCopilotDisplayDriver> stringLocalizer)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditCopilotProfileViewModel>("AIProfileCopilotConfig_Edit", async model =>
        {
            var copilotSettings = profile.As<CopilotProfileSettings>();

            model.CopilotModel = copilotSettings?.CopilotModel;
            model.CopilotFlags = copilotSettings?.CopilotFlags;

            // Check if current user has authenticated with GitHub
            var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User);
            if (user != null)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                model.IsAuthenticated = await _oauthService.IsAuthenticatedAsync(userId);
                if (model.IsAuthenticated)
                {
                    var credential = await _oauthService.GetCredentialAsync(userId);
                    model.GitHubUsername = credential?.GitHubUsername;

                    // Load available models dynamically from GitHub API.
                    var models = await _oauthService.ListModelsAsync(userId);
                    if (models.Count > 0)
                    {
                        model.AvailableModels = models
                            .Select(m => new SelectListItem(m.Name, m.Id))
                            .ToList();
                    }
                }
            }

            model.AvailableModels ??= [];
        }).Location("Content:3.5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditCopilotProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Only save Copilot settings if Copilot orchestrator is selected
        if (string.Equals(profile.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            var copilotSettings = new CopilotProfileSettings
            {
                CopilotModel = model.CopilotModel,
                CopilotFlags = model.CopilotFlags,
            };

            profile.Put(copilotSettings);
        }

        return Edit(profile, context);
    }
}
