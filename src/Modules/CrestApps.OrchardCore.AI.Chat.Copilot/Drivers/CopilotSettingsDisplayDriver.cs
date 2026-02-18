using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Drivers;

public sealed class CopilotSettingsDisplayDriver : SiteDisplayDriver<CopilotSettings>
{
    public const string GroupId = "copilot";

    private const string ProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Copilot.Settings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CopilotSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IHtmlLocalizer<CopilotSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<CopilotSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId => GroupId;

    public override IDisplayResult Edit(ISite site, CopilotSettings settings, BuildEditorContext context)
    {
        return Initialize<CopilotSettingsViewModel>("CopilotSettings_Edit", model =>
        {
            model.ClientId = settings.ClientId;
            model.HasSecret = !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret);

            var request = _httpContextAccessor.HttpContext?.Request;
            if (request != null)
            {
                model.ComputedCallbackUrl = $"{request.Scheme}://{request.Host}/CopilotAuth/OAuthCallback";
            }
        })
        .Location("Content:5")
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
