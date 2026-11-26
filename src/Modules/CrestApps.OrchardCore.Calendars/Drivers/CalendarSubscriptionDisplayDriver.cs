using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Subscription card of the calendar editor, for calendar types that can be connected to an external calendar.
/// </summary>
internal sealed class CalendarSubscriptionDisplayDriver : DisplayDriver<Calendar>
{
    private readonly CalendarSyncService _syncService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public CalendarSubscriptionDisplayDriver(
        CalendarSyncService syncService,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<CalendarSubscriptionDisplayDriver> stringLocalizer)
    {
        _syncService = syncService;
        _typeManager = typeManager;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(Calendar calendar, BuildEditorContext context)
    {
        if (!Supports(calendar))
        {
            return null;
        }

        var subscription = calendar.GetOrCreate<CalendarSubscription>();

        return Initialize<CalendarSubscriptionViewModel>("CalendarSubscription_Edit", model =>
        {
            model.SubscriptionProvider = subscription.Provider ?? ICalendarUrlProvider.ProviderName;
            model.SubscriptionUrl = subscription.Url;
            model.SubscriptionInterval = Math.Max(5, subscription.IntervalMinutes);
            model.SubscriptionEnabled = subscription.Enabled && !string.IsNullOrEmpty(subscription.Url);
            model.LastSyncedUtc = subscription.LastSyncedUtc;
            model.LastError = subscription.LastError;
            model.LastResult = subscription.LastResult;
            model.TwoWay = subscription.TwoWay;
            model.ConflictPolicy = subscription.ConflictPolicy;
            model.NeedsReconnect = subscription.NeedsReconnect;
            model.ProviderOptions = _syncService.ListProviders()
                .Select(provider => new SelectListItem(_localizerFactory.Localize(provider.DisplayName).Value, provider.Name))
                .ToList();
            model.AddressProviders = _syncService.ListProviders().Where(provider => provider.UsesAddress).Select(provider => provider.Name).ToList();
            model.WritableProviders = _syncService.ListProviders().OfType<IWritableCalendarProvider>().Select(provider => provider.Name).ToList();
            model.ConflictPolicyOptions =
            [
                new SelectListItem(S["The external calendar's copy wins"], nameof(CalendarSyncConflictPolicy.ExternalWins)),
                new SelectListItem(S["This calendar's copy wins"], nameof(CalendarSyncConflictPolicy.LocalWins)),
            ];
        }).Location(l => l.Zone("Content", "20").Card(S["Subscription"], "5"));
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        if (!Supports(calendar))
        {
            return null;
        }

        var model = new CalendarSubscriptionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var providerName = string.IsNullOrWhiteSpace(model.SubscriptionProvider) ? ICalendarUrlProvider.ProviderName : model.SubscriptionProvider;
        var provider = _syncService.FindProvider(providerName);
        var usesAddress = provider?.UsesAddress ?? true;
        var url = usesAddress ? model.SubscriptionUrl?.Trim() : null;

        if (model.SubscriptionEnabled && provider is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubscriptionProvider), S["The provider is not available."]);
        }

        if (model.SubscriptionEnabled && usesAddress && !string.IsNullOrEmpty(url) && !CalendarSubscriptionRules.HasAllowedScheme(url))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubscriptionUrl), S["The address must start with https://, http:// or webcal://."]);
        }

        CalendarSubscriptionRules.Apply(calendar, providerName, provider, url, model.SubscriptionInterval, model.TwoWay, model.ConflictPolicy, model.SubscriptionEnabled);

        return Edit(calendar, context);
    }

    private bool Supports(Calendar calendar)
    {
        return CalendarSubscriptionRules.Supports(calendar, _typeManager);
    }
}
