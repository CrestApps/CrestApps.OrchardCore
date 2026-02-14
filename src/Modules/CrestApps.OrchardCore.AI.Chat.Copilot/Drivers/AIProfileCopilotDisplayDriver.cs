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
    private readonly IGitHubOAuthService _oauthService;
    private readonly UserManager<USR.IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AIProfileCopilotDisplayDriver(
        IGitHubOAuthService oauthService,
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
                }
            }

            // Available Copilot models
            // TODO: Query CopilotClient for available models if the SDK exposes this functionality
            // For now, using known models from GitHub Copilot documentation
            // See: https://docs.github.com/en/copilot
            model.AvailableModels =
            [
                new SelectListItem(S["Claude Sonnet 4.5"], "claude-sonnet-4.5"),
                new SelectListItem(S["Claude Haiku 4.5"], "claude-haiku-4.5"),
                new SelectListItem(S["Claude Opus 4.6"], "claude-opus-4.6"),
                new SelectListItem(S["Claude Opus 4.6 Fast"], "claude-opus-4.6-fast"),
                new SelectListItem(S["Claude Opus 4.5"], "claude-opus-4.5"),
                new SelectListItem(S["Claude Sonnet 4"], "claude-sonnet-4"),
                new SelectListItem(S["Gemini 3 Pro Preview"], "gemini-3-pro-preview"),
                new SelectListItem(S["GPT-5.3 Codex"], "gpt-5.3-codex"),
                new SelectListItem(S["GPT-5.2 Codex"], "gpt-5.2-codex"),
                new SelectListItem(S["GPT-5.2"], "gpt-5.2"),
                new SelectListItem(S["GPT-5.1 Codex Max"], "gpt-5.1-codex-max"),
                new SelectListItem(S["GPT-5.1 Codex"], "gpt-5.1-codex"),
                new SelectListItem(S["GPT-5.1"], "gpt-5.1"),
                new SelectListItem(S["GPT-5"], "gpt-5"),
                new SelectListItem(S["GPT-5.1 Codex Mini"], "gpt-5.1-codex-mini"),
                new SelectListItem(S["GPT-5 Mini"], "gpt-5-mini"),
                new SelectListItem(S["GPT-4.1"], "gpt-4.1"),
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
