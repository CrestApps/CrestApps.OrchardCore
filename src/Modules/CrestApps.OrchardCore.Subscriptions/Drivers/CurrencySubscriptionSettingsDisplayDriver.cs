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

public sealed class CurrencySubscriptionSettingsDisplayDriver : SiteDisplayDriver<SubscriptionSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly LinkGenerator _linkGenerator;

    internal IHtmlLocalizer H;
    internal IStringLocalizer S;

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

    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    public override IDisplayResult Edit(ISite site, SubscriptionSettings settings, BuildEditorContext context)
    {
        return Initialize<CurrencySubscriptionSettingsViewModel>("CurrencySubscriptionSettings_Edit", model =>
        {
            // Load the current currency, so we know what is the current value before the save request.
            model.CurrentCurrency = settings.Currency;
        }).Location("Content")
        .OnGroup(SettingsGroupId);
    }

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
