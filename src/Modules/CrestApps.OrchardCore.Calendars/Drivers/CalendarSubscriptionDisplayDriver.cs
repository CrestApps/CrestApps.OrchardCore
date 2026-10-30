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
            model.ProviderOptions = _syncService.ListProviders()
                .Select(provider => new SelectListItem(_localizerFactory.Localize(provider.DisplayName).Value, provider.Name))
                .ToList();
        }).Location("Content:20%Subscription;5");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        if (!Supports(calendar))
        {
            return null;
        }

        var model = new CalendarSubscriptionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var subscription = calendar.GetOrCreate<CalendarSubscription>();
        var url = model.SubscriptionUrl?.Trim();

        if (model.SubscriptionEnabled && !string.IsNullOrEmpty(url) && !(url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubscriptionUrl), S["The address must start with https://, http:// or webcal://."]);
        }

        if (!string.Equals(subscription.Url, url, StringComparison.Ordinal))
        {
            subscription.LastAttemptUtc = null;
            subscription.Failures = 0;
        }

        subscription.Provider = string.IsNullOrWhiteSpace(model.SubscriptionProvider) ? ICalendarUrlProvider.ProviderName : model.SubscriptionProvider;
        subscription.Url = string.IsNullOrEmpty(url) ? null : url;
        subscription.IntervalMinutes = Math.Clamp(model.SubscriptionInterval, 5, 10080);
        subscription.Enabled = model.SubscriptionEnabled && !string.IsNullOrEmpty(url);

        calendar.Put(subscription);

        return Edit(calendar, context);
    }

    private bool Supports(Calendar calendar)
    {
        return string.IsNullOrEmpty(calendar.Source) && (_typeManager.FindType(calendar.Type)?.Supports(CalendarCapabilities.ExternalSync) ?? false);
    }
}
