using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Scheduling.Drivers;

/// <summary>
/// The built-in section of the scheduling resource editor: name, kind, user, calendar, time zone, whether it takes
/// appointments, and where its hours come from. Other modules add their own sections to the editor with drivers of
/// their own for <see cref="SchedulingResource"/>.
/// </summary>
internal sealed class SchedulingResourceFieldsDisplayDriver : DisplayDriver<SchedulingResource>
{
    private readonly ICalendarService _calendarService;
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public SchedulingResourceFieldsDisplayDriver(
        ICalendarService calendarService,
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        ISiteService siteService,
        IStringLocalizer<SchedulingResourceFieldsDisplayDriver> stringLocalizer)
    {
        _calendarService = calendarService;
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SchedulingResource resource, BuildEditorContext context)
    {
        return Initialize<SchedulingResourceViewModel>("SchedulingResourceFields_Edit", async model =>
        {
            model.Name = resource.Name;
            model.Kind = resource.Kind.ToString();
            model.UserId = resource.UserId;
            model.CalendarId = resource.CalendarId;

            // A new resource starts in the site's time zone rather than the first one in the list.
            model.TimeZoneId = resource.TimeZoneId ?? (await _siteService.GetSiteSettingsAsync()).TimeZoneId;
            model.AvailabilityMode = resource.AvailabilityMode.ToString();
            model.ShiftCalendarIds = (resource.ShiftCalendarIds ?? []).Append(resource.ShiftCalendarId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
            model.WorkingHours = SchedulingFormat.WriteHours(resource.WorkingHours);
            model.DateOverrides = SchedulingFormat.WriteOverrides(resource.DateOverrides);
            model.Enabled = resource.Enabled;

            await FillAsync(model);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(SchedulingResource resource, UpdateEditorContext context)
    {
        var posted = new SchedulingResourceViewModel();

        await context.Updater.TryUpdateModelAsync(posted, Prefix);

        var errors = new List<string>();
        var hours = SchedulingFormat.ReadHours(posted.WorkingHours, errors);
        var overrides = SchedulingFormat.ReadOverrides(posted.DateOverrides, errors);

        if (string.IsNullOrWhiteSpace(posted.Name))
        {
            context.Updater.ModelState.AddModelError(Key(nameof(posted.Name)), S["The name is required."]);
        }

        foreach (var error in errors)
        {
            context.Updater.ModelState.AddModelError(Key(nameof(posted.WorkingHours)), S["This line can't be read: {0}", error]);
        }

        if (!string.IsNullOrWhiteSpace(posted.TimeZoneId) && !CalendarTime.IsValidZone(posted.TimeZoneId))
        {
            context.Updater.ModelState.AddModelError(Key(nameof(posted.TimeZoneId)), S["'{0}' is not a time zone.", posted.TimeZoneId]);
        }

        resource.Name = posted.Name?.Trim();
        resource.Kind = Enum.TryParse<SchedulingResourceKind>(posted.Kind, out var kind) ? kind : SchedulingResourceKind.Staff;
        resource.UserId = resource.Kind == SchedulingResourceKind.Staff && !string.IsNullOrWhiteSpace(posted.UserId) ? posted.UserId.Trim() : null;
        resource.CalendarId = string.IsNullOrWhiteSpace(posted.CalendarId) ? null : posted.CalendarId;
        resource.TimeZoneId = CalendarTime.NormalizeZoneId(posted.TimeZoneId) ?? resource.TimeZoneId ?? "UTC";
        resource.AvailabilityMode = Enum.TryParse<SchedulingAvailabilityMode>(posted.AvailabilityMode, out var mode) ? mode : SchedulingAvailabilityMode.WorkingHours;
        resource.ShiftCalendarIds = (posted.ShiftCalendarIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        resource.ShiftCalendarId = null;
        resource.WorkingHours = hours;
        resource.DateOverrides = overrides;
        resource.Enabled = posted.Enabled;

        // The form shows what was posted, so a line that can't be read stays as typed.
        return Initialize<SchedulingResourceViewModel>("SchedulingResourceFields_Edit", async model =>
        {
            model.Name = posted.Name;
            model.Kind = posted.Kind;
            model.UserId = posted.UserId;
            model.CalendarId = posted.CalendarId;
            model.TimeZoneId = posted.TimeZoneId;
            model.AvailabilityMode = posted.AvailabilityMode;
            model.ShiftCalendarIds = posted.ShiftCalendarIds ?? [];
            model.WorkingHours = posted.WorkingHours;
            model.DateOverrides = posted.DateOverrides;
            model.Enabled = posted.Enabled;

            await FillAsync(model);
        }).Location("Content:1");
    }

    private string Key(string name)
        => string.IsNullOrEmpty(Prefix) ? name : $"{Prefix}.{name}";

    private async Task FillAsync(SchedulingResourceViewModel model)
    {
        var calendars = await _calendarService.ListAsync(new CalendarListQuery { Enabled = true, Limit = 500 });

        model.CalendarOptions = calendars.Select(calendar => new SelectListItem($"{calendar.Name} ({calendar.Type})", calendar.ItemId, calendar.ItemId == model.CalendarId)).ToList();
        model.ShiftCalendarOptions = calendars.Select(calendar => new SelectListItem($"{calendar.Name} ({calendar.Type})", calendar.ItemId, model.ShiftCalendarIds?.Contains(calendar.ItemId) == true)).ToList();
        model.KindOptions =
        [
            new SelectListItem(S["Staff member"], nameof(SchedulingResourceKind.Staff)),
            new SelectListItem(S["Room"], nameof(SchedulingResourceKind.Room)),
            new SelectListItem(S["Equipment"], nameof(SchedulingResourceKind.Equipment)),
            new SelectListItem(S["Other"], nameof(SchedulingResourceKind.Other)),
        ];
        model.TimeZoneOptions = await _timeZoneSelectListProvider.GetTimeZoneOptionsAsync(model.TimeZoneId);
        model.InitialUserJson = string.IsNullOrEmpty(model.UserId) ? "[]" : JsonSerializer.Serialize(new[] { new { value = model.UserId, text = model.UserId } });
    }
}
