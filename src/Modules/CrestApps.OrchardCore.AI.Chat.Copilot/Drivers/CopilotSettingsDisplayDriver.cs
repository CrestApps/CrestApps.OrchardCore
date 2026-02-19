using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;

public sealed class CopilotSettingsDisplayDriver : SiteDisplayDriver<CopilotSettings>
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Copilot.Settings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly LinkGenerator _linkGenerator;
    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CopilotSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        LinkGenerator linkGenerator,
        IHtmlLocalizer<CopilotSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<CopilotSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _linkGenerator = linkGenerator;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public override IDisplayResult Edit(ISite site, CopilotSettings settings, BuildEditorContext context)
    {
        return Initialize<CopilotSettingsViewModel>("CopilotSettings_Edit", model =>
        {
            model.ClientId = settings.ClientId;
            model.HasSecret = !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret);
            model.ComputedCallbackUrl = _linkGenerator.GetUriByAction(_httpContextAccessor.HttpContext, "OAuthCallback", "CopilotAuth", new
            {
                area = "CrestApps.OrchardCore.AI.Chat.Copilot",
            });
        })
        .Location("Content:8%Copilot;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, Permissions.ManageCopilotSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, CopilotSettings settings, UpdateEditorContext context)
    {
        var model = new CopilotSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.ClientId = model.ClientId;

        // Validate that client ID and secret are provided
        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            context.Updater.ModelState.AddModelError(nameof(model.ClientId), S["Client ID is required."]);
        }

        // Only update the secret if a new one was provided
        if (!string.IsNullOrWhiteSpace(model.ClientSecret))
        {
            var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
            settings.ProtectedClientSecret = protector.Protect(model.ClientSecret);
        }
        else if (string.IsNullOrWhiteSpace(settings.ProtectedClientSecret))
        {
            // No existing secret and no new secret provided
            context.Updater.ModelState.AddModelError(nameof(model.ClientSecret), S["Client Secret is required."]);
        }

        return await EditAsync(site, settings, context);
    }
}
