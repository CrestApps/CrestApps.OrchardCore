using System.Globalization;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Provides the site settings editor for global subscription options.
/// </summary>
public sealed class SubscriptionSettingsDisplayDriver : SiteDisplayDriver<SubscriptionSettings>
{
    /// <summary>
    /// The settings group identifier used for subscription settings.
    /// </summary>
    public const string GroupId = "subscriptions";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly PaymentMethodOptions _paymentMethodOptions;

    internal IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the current user.</param>
    /// <param name="authorizationService">The authorization service used to check access to subscription settings.</param>
    /// <param name="paymentMethodOptions">The configured payment methods available to subscriptions.</param>
    /// <param name="shellReleaseManager">The shell release manager used to restart the tenant after settings changes.</param>
    /// <param name="stringLocalizer">The localizer used for validation messages.</param>
    public SubscriptionSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptions<PaymentMethodOptions> paymentMethodOptions,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<SubscriptionSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
        _paymentMethodOptions = paymentMethodOptions.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the settings group used to render subscription settings.
    /// </summary>
    protected override string SettingsGroupId
        => GroupId;

    /// <summary>
    /// Builds the editor shape for subscription settings when the current user is authorized.
    /// </summary>
    /// <param name="model">The site being edited.</param>
    /// <param name="settings">The subscription settings to display.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result for the subscription settings editor, or <see langword="null"/> when access is denied.</returns>
    public override async Task<IDisplayResult> EditAsync(ISite model, SubscriptionSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        context.AddTenantReloadWarningWrapper();

        return Initialize<SubscriptionSettingsViewModel>("SubscriptionSettings_Edit", model =>
        {
            model.DefaultPaymentMethod = settings.DefaultPaymentMethod;
            model.AllowGuestSignup = settings.AllowGuestSignup;
            model.Currency = settings.Currency;
            model.Currencies = GetCurrencies();
            model.PaymentMethods = _paymentMethodOptions.PaymentMethods
            .Select(m => new SelectListItem(m.Value.Title, m.Key))
            .OrderBy(m => m.Text);
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    /// <summary>
    /// Updates subscription settings from the posted site settings values and requests a tenant reload.
    /// </summary>
    /// <param name="site">The site being updated.</param>
    /// <param name="settings">The subscription settings to update.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The updated editor display result, or <see langword="null"/> when access is denied.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, SubscriptionSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        var model = new SubscriptionSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.Currency))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Currency), S["Currency is required field."]);
        }
        else if (!GetCurrencies().Any(x => x.Value == model.Currency))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Currency), S["Invalid currency value."]);
        }
        else
        {
            // Update the currency only if a valid value is provided, as other components may depend on the existing values.
            settings.Currency = model.Currency;
        }

        var providedPaymentMethod = !string.IsNullOrEmpty(model.DefaultPaymentMethod);

        if (_paymentMethodOptions.PaymentMethods.Count > 1 && !providedPaymentMethod)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PaymentMethods), S["Default Payment Method is required."]);
        }
        else if (providedPaymentMethod && !_paymentMethodOptions.PaymentMethods.ContainsKey(model.DefaultPaymentMethod))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PaymentMethods), S["Invalid Default Payment Method."]);
        }

        settings.DefaultPaymentMethod = model.DefaultPaymentMethod;
        settings.AllowGuestSignup = model.AllowGuestSignup;

        _shellReleaseManager.RequestRelease();

        return await EditAsync(site, settings, context);
    }

    private static SelectListItem[] _currencies;

    private static SelectListItem[] GetCurrencies()
    {
        if (_currencies == null)
        {
            var currencies = new Dictionary<string, SelectListItem>();

            foreach (var cultureInfo in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                var regionInfo = new RegionInfo(cultureInfo.Name);
                var currencyCode = regionInfo.ISOCurrencySymbol;
                if (string.IsNullOrEmpty(regionInfo.CurrencyEnglishName) || string.IsNullOrEmpty(currencyCode))
                {
                    continue;
                }

                if (!currencies.ContainsKey(currencyCode))
                {
                    currencies.Add(currencyCode, new SelectListItem(regionInfo.CurrencyEnglishName, currencyCode));
                }
            }

            _currencies = currencies.Values.OrderBy(x => x.Text).ToArray();
        }

        return _currencies;
    }
}
