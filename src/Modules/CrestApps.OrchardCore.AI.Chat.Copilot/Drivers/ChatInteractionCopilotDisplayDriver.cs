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

internal sealed class ChatInteractionCopilotDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IGitHubOAuthService _oauthService;
    private readonly UserManager<USR.IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ChatInteractionCopilotDisplayDriver(
        IGitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ChatInteractionCopilotDisplayDriver> stringLocalizer)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        return Initialize<EditCopilotProfileViewModel>("ChatInteractionCopilotConfig_Edit", async model =>
        {
            var copilotSettings = interaction.As<CopilotProfileSettings>();

            model.CopilotModel = copilotSettings?.CopilotModel;
            model.CopilotFlags = copilotSettings?.CopilotFlags;

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
                            .Select(m => new SelectListItem(m.Name, m.Id))
                            .ToList();
                    }
                }
            }

            model.AvailableModels ??= [];
        }).Location("Parameters:1#Settings:2.5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new EditCopilotProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.Equals(interaction.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            var copilotSettings = new CopilotProfileSettings
            {
                CopilotModel = model.CopilotModel,
                CopilotFlags = model.CopilotFlags,
            };

            interaction.Put(copilotSettings);
        }

        return Edit(interaction, context);
    }
}
