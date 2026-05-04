using CrestApps.Core;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;
using USR = OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;

internal sealed class AIProfileCopilotDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly GitHubOAuthService _oauthService;
    private readonly UserManager<USR.IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileCopilotDisplayDriver"/> class.
    /// </summary>
    /// <param name="oauthService">The oauth service.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileCopilotDisplayDriver(
        GitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        IStringLocalizer<AIProfileCopilotDisplayDriver> stringLocalizer)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditCopilotProfileViewModel>("AIProfileCopilotConfig_Edit", async model =>
        {
            var copilotSettings = profile.GetOrCreate<CopilotSessionMetadata>();

            model.CopilotModel = copilotSettings.CopilotModel;
            model.IsAllowAll = copilotSettings.IsAllowAll;
            model.CopilotReasoningEffort = copilotSettings.ReasoningEffort;

            // Load site-level settings to determine auth mode.
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
                // BYOK mode — no GitHub auth needed; model is a text input.
                model.AvailableModels = [];
            }
            else if (siteSettings.AuthenticationType == CopilotAuthenticationType.GitHubOAuth)
            {
                // GitHub OAuth mode — check auth and load models from GitHub API.
                var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User);

                if (user != null)
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
        }).Location("Content:3.5%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditCopilotProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Only save Copilot settings if Copilot orchestrator is selected

        if (string.Equals(profile.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            var copilotSettings = new CopilotSessionMetadata
            {
                CopilotModel = model.CopilotModel,
                IsAllowAll = model.IsAllowAll,
                ReasoningEffort = model.CopilotReasoningEffort,
            };

            var siteSettings = await _siteService.GetSettingsAsync<CopilotSettings>();

            if (siteSettings.AuthenticationType == CopilotAuthenticationType.GitHubOAuth)
            {
                // Copy the current user's GitHub credential to the profile so
                // any chat session using this profile can reuse the token.
                var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User);

                if (user is not null)
                {
                    var userId = await _userManager.GetUserIdAsync(user);
                    var credentials = await _oauthService.GetProtectedCredentialsAsync(userId);

                    if (credentials is not null)
                    {
                        copilotSettings.GitHubUsername = credentials.GitHubUsername;
                        copilotSettings.ProtectedAccessToken = credentials.ProtectedAccessToken;
                        copilotSettings.ProtectedRefreshToken = credentials.ProtectedRefreshToken;
                        copilotSettings.ExpiresAt = credentials.ExpiresAt;
                    }
                }
            }

            profile.Put(copilotSettings);
        }

        return Edit(profile, context);
    }
}
