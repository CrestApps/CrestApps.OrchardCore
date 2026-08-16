using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Displays and updates currency-specific subscription settings.
/// </summary>
public sealed class CurrencySubscriptionSettingsDisplayDriver : SiteDisplayDriver<SubscriptionSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly LinkGenerator _linkGenerator;

    internal IHtmlLocalizer H;
    internal IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencySubscriptionSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor used to read the current HTTP context.</param>
    /// <param name="authorizationService">The authorization service used to check settings permissions.</param>
    /// <param name="notifier">The notifier used to display currency change warnings.</param>
    /// <param name="linkGenerator">The link generator used to build the Stripe synchronization URL.</param>
    /// <param name="htmlLocalizer">The HTML localizer for notification text.</param>
    /// <param name="stringLocalizer">The string localizer for settings text.</param>
    public CurrencySubscriptionSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        INotifier notifier,
        LinkGenerator linkGenerator,
        IHtmlLocalizer<CurrencySubscriptionSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<SubscriptionSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _linkGenerator = linkGenerator;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the settings group identifier used by subscription settings.
    /// </summary>
    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    /// <summary>
    /// Builds the editor for the subscription currency settings.
    /// </summary>
    /// <param name="site">The site entity that owns the settings.</param>
    /// <param name="settings">The current subscription settings.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result for the currency settings editor.</returns>
    public override IDisplayResult Edit(ISite site, SubscriptionSettings settings, BuildEditorContext context)
    {
        return Initialize<CurrencySubscriptionSettingsViewModel>("CurrencySubscriptionSettings_Edit", model =>
        {
            // Load the current currency, so we know what is the current value before the save request.
            model.CurrentCurrency = settings.Currency;
        }).Location("Content")
        .OnGroup(SettingsGroupId);
    }

    /// <summary>
    /// Updates the subscription currency settings and warns when Stripe prices must be synchronized.
    /// </summary>
    /// <param name="site">The site entity that owns the settings.</param>
    /// <param name="settings">The subscription settings being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The updated display result, or <see langword="null"/> when the user lacks permission.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, SubscriptionSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        var model = new CurrencySubscriptionSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var defaultModel = new SubscriptionSettingsViewModel();

        // Use the same prefix, as both models share the 'SubscriptionSettings' type.
        await context.Updater.TryUpdateModelAsync(defaultModel, Prefix);

        if (!string.IsNullOrEmpty(defaultModel.Currency) && defaultModel.Currency != model.CurrentCurrency)
        {
            var url = _linkGenerator.GetPathByName(_httpContextAccessor.HttpContext, "StripeSyncPrices", new
            {
                area = SubscriptionConstants.Features.Area,
            });

            await _notifier.WarningAsync(H["Since the currency has changed, it's important to update all Price items in Stripe. Click <a href=\"{0}\">here</a> to sync all Stripe price items.", url]);
        }

        return await EditAsync(site, settings, context);
    }
}
