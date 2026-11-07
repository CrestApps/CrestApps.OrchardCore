using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits what the phone lines cost, on the AI settings page, for the estimated cost of each AI call. What the AI costs
/// is set on each AI deployment.
/// </summary>
public sealed class AICostSettingsDisplayDriver : SiteDisplayDriver<AICostSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <inheritdoc/>
    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICostSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AICostSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<AICostSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, AICostSettings settings, BuildEditorContext context)
    {
        return Initialize<AICostSettingsViewModel>("AICostSettings_Edit", model =>
        {
            model.Currency = settings.Currency;
            model.TelephonyPerMinute = settings.TelephonyPerMinute;
            model.SmsPerMessage = settings.SmsPerMessage;
        }).Location("Content:9%Costs;9")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, AICostSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new AICostSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant();
        settings.TelephonyPerMinute = Math.Max(0, model.TelephonyPerMinute);
        settings.SmsPerMessage = Math.Max(0, model.SmsPerMessage);

        return Edit(site, settings, context);
    }
}
