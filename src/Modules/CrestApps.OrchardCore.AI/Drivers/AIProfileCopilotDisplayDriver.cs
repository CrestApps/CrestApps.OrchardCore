using CrestApps.OrchardCore.AI.Chat.Copilot;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileCopilotDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IGitHubOAuthService _oauthService;
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStringLocalizer S;

    public AIProfileCopilotDisplayDriver(
        IGitHubOAuthService oauthService,
        IUserService userService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIProfileCopilotDisplayDriver> stringLocalizer)
    {
        _oauthService = oauthService;
        _userService = userService;
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
            var user = await _userService.GetAuthenticatedUserAsync(_httpContextAccessor.HttpContext?.User);
            if (user != null)
            {
                model.IsAuthenticated = await _oauthService.IsAuthenticatedAsync(user.UserId);
                if (model.IsAuthenticated)
                {
                    var credential = await _oauthService.GetCredentialAsync(user.UserId);
                    model.GitHubUsername = credential?.GitHubUsername;
                }
            }

            // Available Copilot models
            // Based on GitHub Copilot SDK documentation
            model.AvailableModels =
            [
                new SelectListItem(S["GPT-4o"], "gpt-4o"),
                new SelectListItem(S["GPT-4o Mini"], "gpt-4o-mini"),
                new SelectListItem(S["Claude 3.5 Sonnet"], "claude-3.5-sonnet"),
                new SelectListItem(S["o1-preview"], "o1-preview"),
                new SelectListItem(S["o1-mini"], "o1-mini"),
            ];
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

/// <summary>
/// Settings specific to Copilot orchestrator configuration.
/// </summary>
public sealed class CopilotProfileSettings
{
    /// <summary>
    /// The Copilot model to use (e.g., gpt-4o, claude-3.5-sonnet).
    /// </summary>
    public string CopilotModel { get; set; }

    /// <summary>
    /// Additional Copilot execution flags (e.g., --allow-all).
    /// </summary>
    public string CopilotFlags { get; set; }
}
