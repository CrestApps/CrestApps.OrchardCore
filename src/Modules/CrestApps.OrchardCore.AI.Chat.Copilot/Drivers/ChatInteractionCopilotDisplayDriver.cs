using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;

internal sealed class ChatInteractionCopilotDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly GitHubOAuthService _oauthService;
    private readonly UserManager<IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public ChatInteractionCopilotDisplayDriver(
        GitHubOAuthService oauthService,
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        IStringLocalizer<ChatInteractionCopilotDisplayDriver> stringLocalizer)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<EditCopilotProfileViewModel>("ChatInteractionCopilotConfig_Edit", async model =>
        {
            var copilotSettings = interaction.As<CopilotSessionMetadata>();

            model.CopilotModel = copilotSettings.CopilotModel;
            model.IsAllowAll = copilotSettings.IsAllowAll;

            // Load site-level settings to determine auth mode.
            var siteSettings = await _siteService.GetSettingsAsync<CopilotSettings>();
            model.AuthenticationType = siteSettings.AuthenticationType;

            if (siteSettings.AuthenticationType == CopilotAuthenticationType.ApiKey)
            {
                // BYOK mode — no GitHub auth needed.
                model.AvailableModels = [];
            }
            else
            {
                // GitHub OAuth mode — only fetch auth/models when the orchestrator is Copilot.
                if (string.Equals(interaction.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase) &&
                    _httpContextAccessor.HttpContext?.User is not null)
                {
                    var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext.User);

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
                                    .Select(m => new SelectListItem(m.Name, m.Id))
                                    .ToList();
                            }
                        }
                    }
                }

                model.AvailableModels ??= [];
            }
        }).Location("Parameters:4#Settings;1");
    }
}
