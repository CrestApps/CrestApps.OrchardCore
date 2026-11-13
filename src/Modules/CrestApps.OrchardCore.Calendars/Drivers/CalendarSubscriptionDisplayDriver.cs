using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Hosting;
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
    private readonly IHostEnvironment _hostEnvironment;

    internal readonly IStringLocalizer S;

    public CalendarSubscriptionDisplayDriver(
        CalendarSyncService syncService,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory,
        IHostEnvironment hostEnvironment,
        IStringLocalizer<CalendarSubscriptionDisplayDriver> stringLocalizer)
    {
        _syncService = syncService;
        _typeManager = typeManager;
        _localizerFactory = localizerFactory;
        _hostEnvironment = hostEnvironment;
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
        var providerName = string.IsNullOrWhiteSpace(model.SubscriptionProvider) ? ICalendarUrlProvider.ProviderName : model.SubscriptionProvider;
        var provider = _syncService.FindProvider(providerName);
        var usesAddress = provider?.UsesAddress ?? true;
        var url = usesAddress ? model.SubscriptionUrl?.Trim() : null;

        if (model.SubscriptionEnabled && provider is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubscriptionProvider), S["The provider is not available."]);
        }

        // The sync task fetches the address from the server, so one on the site's own network is refused here as well as
        // at connect.
        if (model.SubscriptionEnabled && usesAddress && !string.IsNullOrEmpty(url) && !IsAllowedAddress(url, _hostEnvironment.IsDevelopment()))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubscriptionUrl), S["The address must start with https://, http:// or webcal://, and can't be on the site's own network."]);
        }

        if (!string.Equals(subscription.Url, url, StringComparison.Ordinal) || !string.Equals(subscription.Provider, providerName, StringComparison.OrdinalIgnoreCase))
        {
            subscription.LastAttemptUtc = null;
            subscription.Failures = 0;
            subscription.SyncToken = null;
            subscription.PendingDeletes.Clear();
        }

        subscription.Provider = providerName;
        subscription.Url = string.IsNullOrEmpty(url) ? null : url;
        subscription.IntervalMinutes = Math.Clamp(model.SubscriptionInterval, 5, 10080);
        subscription.TwoWay = model.TwoWay && provider is IWritableCalendarProvider;
        subscription.ConflictPolicy = model.ConflictPolicy;
        subscription.Enabled = model.SubscriptionEnabled && (provider?.IsConfigured(calendar, subscription) ?? false);

        // Saving the subscription again is how someone says the account was reconnected.
        if (subscription.NeedsReconnect)
        {
            subscription.NeedsReconnect = false;
            subscription.LastAttemptUtc = null;
            subscription.Failures = 0;
        }

        calendar.Put(subscription);

        return Edit(calendar, context);
    }

    /// <summary>
    /// Gets whether a calendar may subscribe to an address: a public https://, http:// or webcal:// address (webcal is
    /// fetched over HTTPS), never one on the site's own network. The sync fetches http:// as well, so a calendar with a
    /// stored http:// feed can still be saved.
    /// </summary>
    /// <param name="url">The address.</param>
    /// <param name="isDevelopment">Whether the site runs in the Development environment.</param>
    internal static bool IsAllowedAddress(string url, bool isDevelopment)
    {
        var address = url?.Trim() ?? string.Empty;

        if (address.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            address = string.Concat("https://", address.AsSpan(9));
        }

        return EgressNetwork.IsExternalUrl(address, isDevelopment);
    }

    private bool Supports(Calendar calendar)
    {
        return string.IsNullOrEmpty(calendar.Source) && (_typeManager.FindType(calendar.Type)?.Supports(CalendarCapabilities.ExternalSync) ?? false);
    }
}
