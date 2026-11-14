using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Drivers;

/// <summary>
/// Edits what the phone carrier charges, on the telephony settings page, for the estimated cost of each call.
/// </summary>
public sealed class TelephonyCostSettingsDisplayDriver : SiteDisplayDriver<TelephonyCostSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyCostSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public TelephonyCostSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, TelephonyCostSettings settings, BuildEditorContext context)
    {
        return Initialize<TelephonyCostSettingsViewModel>("TelephonyCostSettings_Edit", model =>
        {
            model.Currency = settings.Currency;
            model.PerMinute = settings.PerMinute;
        }).Location("Content:3#Costs")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, TelephonyCostSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new TelephonyCostSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant();
        settings.PerMinute = Math.Max(0, model.PerMinute);

        return Edit(site, settings, context);
    }
}
