using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Dispatch.Drivers;

/// <summary>
/// Reads the dispatch settings from the site settings.
/// </summary>
public sealed class SiteDispatchSettingsProvider : IDispatchSettingsProvider
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteDispatchSettingsProvider"/> class.
    /// </summary>
    public SiteDispatchSettingsProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public Task<DispatchSettings> GetAsync() => _siteService.GetSettingsAsync<DispatchSettings>();

    /// <inheritdoc/>
    public async Task<string> GetTimeZoneIdAsync()
    {
        var zone = (await GetAsync()).TimeZoneId;

        return string.IsNullOrEmpty(zone) ? (await _siteService.GetSiteSettingsAsync()).TimeZoneId : zone;
    }
}

/// <summary>
/// Shows and saves the dispatch settings.
/// </summary>
public sealed class DispatchSettingsDisplayDriver : SiteDisplayDriver<DispatchSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IVisitNoticeSender> _senders;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchSettingsDisplayDriver"/> class.
    /// </summary>
    public DispatchSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IEnumerable<IVisitNoticeSender> senders, IStringLocalizer<DispatchSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _senders = senders;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId => DispatchConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, DispatchSettings settings, BuildEditorContext context)
        => Initialize<DispatchSettingsViewModel>("DispatchSettings_Edit", model =>
        {
            var objective = settings.Objective ?? new DispatchObjective();

            model.CreateVisitsForNewWorkOrders = settings.CreateVisitsForNewWorkOrders;
            model.ArrivalWindowMinutes = settings.ArrivalWindowMinutes;
            model.HorizonDays = settings.HorizonDays;
            model.BufferMinutes = settings.BufferMinutes;
            model.StrictTerritory = settings.StrictTerritory;
            model.TimeZoneId = settings.TimeZoneId;
            model.TravelWeight = objective.TravelWeight;
            model.UrgencyWeight = objective.UrgencyWeight;
            model.ProficiencyWeight = objective.ProficiencyWeight;
            model.PreferredWeight = objective.PreferredWeight;
            model.BalanceWeight = objective.BalanceWeight;
            model.AtRiskMinutes = settings.AtRiskMinutes;
            model.OptimizerSeconds = settings.OptimizerSeconds;
            model.NoticesEnabled = settings.NoticesEnabled;
            model.TrackingEnabled = settings.TrackingEnabled;
            model.ReminderHour = settings.ReminderHour;
            model.ScheduledText = settings.ScheduledText;
            model.ReminderText = settings.ReminderText;
            model.EnRouteText = settings.EnRouteText;
            model.CompletedText = settings.CompletedText;
            model.CanceledText = settings.CanceledText;
            model.CanSendNotices = _senders.Any();
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, DispatchPermissions.ManageDispatchSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, DispatchSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, DispatchPermissions.ManageDispatchSettings))
        {
            return null;
        }

        var model = new DispatchSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.ArrivalWindowMinutes is < 0 or > 720)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ArrivalWindowMinutes), S["The arrival window is from 0 to 720 minutes."]);
        }

        if (model.HorizonDays is < 1 or > 60)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.HorizonDays), S["Look from 1 to 60 days ahead."]);
        }

        if (model.BufferMinutes is < 0 or > 120)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.BufferMinutes), S["The buffer is from 0 to 120 minutes."]);
        }

        foreach (var (name, weight) in new[] { (nameof(model.TravelWeight), model.TravelWeight), (nameof(model.UrgencyWeight), model.UrgencyWeight), (nameof(model.ProficiencyWeight), model.ProficiencyWeight), (nameof(model.PreferredWeight), model.PreferredWeight), (nameof(model.BalanceWeight), model.BalanceWeight) })
        {
            if (weight is < 0 or > 10)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + name, S["A weight is from 0 to 10."]);
            }
        }

        if (!string.IsNullOrWhiteSpace(model.TimeZoneId) && !Calendars.CalendarTime.IsValidZone(model.TimeZoneId.Trim()))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.TimeZoneId), S["'{0}' isn't a time zone.", model.TimeZoneId]);
        }

        if (model.AtRiskMinutes is < 0 or > 240)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.AtRiskMinutes), S["A visit is at risk from 0 to 240 minutes late."]);
        }

        if (model.OptimizerSeconds is < 1 or > 120)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.OptimizerSeconds), S["The optimizer looks for 1 to 120 seconds."]);
        }

        if (model.ReminderHour is < 0 or > 23)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ReminderHour), S["The reminder hour is from 0 to 23."]);
        }

        settings.AtRiskMinutes = model.AtRiskMinutes;
        settings.OptimizerSeconds = model.OptimizerSeconds;
        settings.NoticesEnabled = model.NoticesEnabled;
        settings.TrackingEnabled = model.TrackingEnabled;
        settings.ReminderHour = model.ReminderHour;
        settings.ScheduledText = Text(model.ScheduledText, settings.ScheduledText);
        settings.ReminderText = Text(model.ReminderText, settings.ReminderText);
        settings.EnRouteText = Text(model.EnRouteText, settings.EnRouteText);
        settings.CompletedText = Text(model.CompletedText, settings.CompletedText);
        settings.CanceledText = Text(model.CanceledText, settings.CanceledText);
        settings.CreateVisitsForNewWorkOrders = model.CreateVisitsForNewWorkOrders;
        settings.ArrivalWindowMinutes = model.ArrivalWindowMinutes;
        settings.HorizonDays = model.HorizonDays;
        settings.BufferMinutes = model.BufferMinutes;
        settings.StrictTerritory = model.StrictTerritory;
        settings.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? null : model.TimeZoneId.Trim();
        settings.Objective = new DispatchObjective
        {
            TravelWeight = model.TravelWeight,
            UrgencyWeight = model.UrgencyWeight,
            ProficiencyWeight = model.ProficiencyWeight,
            PreferredWeight = model.PreferredWeight,
            BalanceWeight = model.BalanceWeight,
        };

        return await EditAsync(site, settings, context);
    }

    private static string Text(string value, string current) => string.IsNullOrWhiteSpace(value) ? current : value.Trim();
}
