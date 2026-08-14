using CrestApps.Core.AI.Claude.Models;
using CrestApps.Core.AI.Claude.Services;
using CrestApps.OrchardCore.AI.Chat.Claude.Services;
using CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Claude.Drivers;

/// <summary>
/// Display driver for the claude settings shape.
/// </summary>
public sealed class ClaudeSettingsDisplayDriver : SiteDisplayDriver<ClaudeSettings>
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Claude.Settings";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ClaudeClientService _claudeClientService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="claudeClientService">The claude client service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ClaudeSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        ClaudeClientService claudeClientService,
        IStringLocalizer<ClaudeSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _claudeClientService = claudeClientService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public override IDisplayResult Edit(ISite site, ClaudeSettings settings, BuildEditorContext context)
    {
        return Initialize<ClaudeSettingsViewModel>("ClaudeSettings_Edit", async model =>
        {
            model.AuthenticationType = settings.AuthenticationType;
            model.BaseUrl = settings.BaseUrl;
            model.DefaultModel = settings.DefaultModel;
            model.HasApiKey = !string.IsNullOrWhiteSpace(settings.ProtectedApiKey);
            model.AuthenticationTypes =
            [
                new SelectListItem(S["Not configured"], nameof(ClaudeAuthenticationType.NotConfigured)),
                new SelectListItem(S["API key"], nameof(ClaudeAuthenticationType.ApiKey)),
            ];

            if (settings.AuthenticationType == ClaudeAuthenticationType.ApiKey &&
                model.HasApiKey)
            {
                var models = await _claudeClientService.ListModelsAsync();
                model.AvailableModels = ClaudeModelSelectListFactory.Build(models, settings.DefaultModel);
            }
            else
            {
                model.AvailableModels = ClaudeModelSelectListFactory.Build([], settings.DefaultModel);
            }
        })
        .Location("Content:9%Claude;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ClaudePermissionProvider.ManageClaudeSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ClaudeSettings settings, UpdateEditorContext context)
    {
        var model = new ClaudeSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.AuthenticationType = model.AuthenticationType;
        settings.BaseUrl = string.IsNullOrWhiteSpace(model.BaseUrl) ? "https://api.anthropic.com" : model.BaseUrl.Trim();
        settings.DefaultModel = model.DefaultModel?.Trim();

        if (settings.AuthenticationType == ClaudeAuthenticationType.ApiKey)
        {
            if (!string.IsNullOrWhiteSpace(model.ApiKey))
            {
                var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
                settings.ProtectedApiKey = protector.Protect(model.ApiKey);
            }
            else if (string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required."]);
            }
        }

        return await EditAsync(site, settings, context);
    }
}
