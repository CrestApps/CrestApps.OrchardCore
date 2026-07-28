using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for shared AI chat prompt security settings.
/// </summary>
public sealed class PromptSecurityOptionsDisplayDriver : SiteDisplayDriver<PromptSecurityOptions>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptSecurityOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PromptSecurityOptionsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<PromptSecurityOptionsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, PromptSecurityOptions settings, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<PromptSecurityOptionsViewModel>("PromptSecurityOptions_Edit", model =>
        {
            model.EnableInjectionDetection = settings.EnableInjectionDetection;
            model.EnableOutputFiltering = settings.EnableOutputFiltering;
            model.EnableSecurityPreamble = settings.EnableSecurityPreamble;
            model.EnableInputDelimiters = settings.EnableInputDelimiters;
            model.EnableAuditLogging = settings.EnableAuditLogging;
            model.MaxPromptLength = settings.MaxPromptLength;
            model.BlockingThreshold = settings.BlockingThreshold;
            model.MaxMessagesPerWindow = settings.MaxMessagesPerWindow;
            model.RateLimitWindowSeconds = (int)Math.Round(settings.RateLimitWindow.TotalSeconds);
            model.MaxAnonymousSessionsPerWindow = settings.MaxAnonymousSessionsPerWindow;
            model.AnonymousSessionRateLimitWindowSeconds = (int)Math.Round(settings.AnonymousSessionRateLimitWindow.TotalSeconds);
        }).Location("Content:2%Prompt Security;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, PromptSecurityOptions settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new PromptSecurityOptionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MaxPromptLength < 1 || model.MaxPromptLength > 100_000)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxPromptLength), S["Maximum prompt length must be between {0} and {1}.", 1, 100_000]);
        }

        if (model.MaxMessagesPerWindow < 0 || model.MaxMessagesPerWindow > 1_000)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxMessagesPerWindow), S["Maximum messages per window must be between {0} and {1}.", 0, 1_000]);
        }

        if (model.RateLimitWindowSeconds < 1 || model.RateLimitWindowSeconds > 86_400)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RateLimitWindowSeconds), S["Rate-limit window must be between {0} and {1} second(s).", 1, 86_400]);
        }

        if (model.MaxAnonymousSessionsPerWindow < 0 || model.MaxAnonymousSessionsPerWindow > 1_000)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxAnonymousSessionsPerWindow), S["Maximum anonymous sessions per window must be between {0} and {1}.", 0, 1_000]);
        }

        if (model.AnonymousSessionRateLimitWindowSeconds < 1 || model.AnonymousSessionRateLimitWindowSeconds > 86_400)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AnonymousSessionRateLimitWindowSeconds), S["Anonymous session window must be between {0} and {1} second(s).", 1, 86_400]);
        }

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(site, settings, context);
        }

        var rateLimitWindow = TimeSpan.FromSeconds(model.RateLimitWindowSeconds);
        var anonymousSessionRateLimitWindow = TimeSpan.FromSeconds(model.AnonymousSessionRateLimitWindowSeconds);
        var settingsChanged =
            settings.EnableInjectionDetection != model.EnableInjectionDetection ||
            settings.EnableOutputFiltering != model.EnableOutputFiltering ||
            settings.EnableSecurityPreamble != model.EnableSecurityPreamble ||
            settings.EnableInputDelimiters != model.EnableInputDelimiters ||
            settings.EnableAuditLogging != model.EnableAuditLogging ||
            settings.MaxPromptLength != model.MaxPromptLength ||
            settings.BlockingThreshold != model.BlockingThreshold ||
            settings.MaxMessagesPerWindow != model.MaxMessagesPerWindow ||
            settings.RateLimitWindow != rateLimitWindow ||
            settings.MaxAnonymousSessionsPerWindow != model.MaxAnonymousSessionsPerWindow ||
            settings.AnonymousSessionRateLimitWindow != anonymousSessionRateLimitWindow;

        settings.EnableInjectionDetection = model.EnableInjectionDetection;
        settings.EnableOutputFiltering = model.EnableOutputFiltering;
        settings.EnableSecurityPreamble = model.EnableSecurityPreamble;
        settings.EnableInputDelimiters = model.EnableInputDelimiters;
        settings.EnableAuditLogging = model.EnableAuditLogging;
        settings.MaxPromptLength = model.MaxPromptLength;
        settings.BlockingThreshold = model.BlockingThreshold;
        settings.MaxMessagesPerWindow = model.MaxMessagesPerWindow;
        settings.RateLimitWindow = rateLimitWindow;
        settings.MaxAnonymousSessionsPerWindow = model.MaxAnonymousSessionsPerWindow;
        settings.AnonymousSessionRateLimitWindow = anonymousSessionRateLimitWindow;

        if (settingsChanged)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
