using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
    private readonly IOptions<Staff.SchedulingStaffOptions> _staffOptions;

    internal readonly IStringLocalizer S;

    public SchedulingResourceFieldsDisplayDriver(
        ICalendarService calendarService,
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        ISiteService siteService,
        IOptions<Staff.SchedulingStaffOptions> staffOptions,
        IStringLocalizer<SchedulingResourceFieldsDisplayDriver> stringLocalizer)
    {
        _staffOptions = staffOptions;
        _calendarService = calendarService;
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SchedulingResource resource, BuildEditorContext context)
    {
        return Build(async model =>
        {
            // A new resource starts in the site's time zone rather than the first one in the list.
            SchedulingResourceEditorRules.Fill(model, resource, resource.TimeZoneId ?? (await _siteService.GetSiteSettingsAsync()).TimeZoneId);

            await FillAsync(model);
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(SchedulingResource resource, UpdateEditorContext context)
    {
        var posted = new SchedulingResourceViewModel();

        await context.Updater.TryUpdateModelAsync(posted, Prefix);

        foreach (var error in SchedulingResourceEditorRules.Apply(resource, posted, S))
        {
            context.Updater.ModelState.AddModelError(Key(error.Key), error.Value);
        }

        // The form shows what was posted, so a line that can't be read stays as typed.
        return Build(async model =>
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
        });
    }

    // One shape per card, each a copy of the same view model so the fields bind once.
    private CombinedResult Build(Func<SchedulingResourceViewModel, ValueTask> populate)
    {
        return Combine(
            Initialize<SchedulingResourceViewModel>("SchedulingResourceFields_Edit", populate)
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<SchedulingResourceViewModel>("SchedulingResourceHours_Edit", populate)
                .Location(l => l.Zone("Content", "1").Card(S["Hours"], "20")));
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
        model.UseStaffPicker = _staffOptions.Value.UseStaffPicker;
    }
}
