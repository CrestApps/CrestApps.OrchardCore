using System.Globalization;
using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.ViewModels;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.BusinessHours.Drivers;

/// <summary>
/// The schedule's row in the list of schedules, and the cards of its editor: general, weekly hours and special days.
/// Other modules add cards with their own drivers.
/// </summary>
internal sealed class BusinessHoursScheduleDisplayDriver : DisplayDriver<BusinessHoursSchedule>
{
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;

    internal readonly IStringLocalizer S;

    public BusinessHoursScheduleDisplayDriver(
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        IStringLocalizer<BusinessHoursScheduleDisplayDriver> stringLocalizer)
    {
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(BusinessHoursSchedule schedule, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = schedule.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("BusinessHoursSchedule_Fields_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("BusinessHoursSchedule_Description_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(schedule.Description))),
            View("BusinessHoursSchedule_Tags_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Hours:5"),
            View("BusinessHoursSchedule_Buttons_SummaryAdmin", schedule).Location("Actions:5"),
            View("BusinessHoursSchedule_ActionsMenu_SummaryAdmin", schedule).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(BusinessHoursSchedule schedule, BuildEditorContext context)
    {
        async ValueTask PopulateAsync(BusinessHoursScheduleViewModel model)
        {
            var hours = schedule.Hours ?? new HoursSchedule();

            model.Name = schedule.Name;
            model.Description = schedule.Description;
            model.Enabled = schedule.Enabled;
            model.TimeZoneId = hours.TimeZoneId;
            model.Days = WeeklyHoursRows.FromWindows(hours.Windows);
            model.SpecialDays = HoursDateExceptionText.Format(hours.DateExceptions);
            model.TimeZoneOptions = await _timeZoneSelectListProvider.GetTimeZoneOptionsAsync(hours.TimeZoneId);

            // Only a new schedule starts from office hours; a saved one with no open day is closed all week.
            if (!context.IsNew && hours.Windows.Count == 0)
            {
                foreach (var day in model.Days)
                {
                    day.Open = false;
                }
            }
        }

        return Combine(
            Initialize<BusinessHoursScheduleViewModel>("BusinessHoursScheduleGeneral_Edit", PopulateAsync).Location("Content:1%General;1"),
            Initialize<BusinessHoursScheduleViewModel>("BusinessHoursScheduleWeekly_Edit", PopulateAsync).Location("Content:1%Weekly hours;2"),
            Initialize<BusinessHoursScheduleViewModel>("BusinessHoursScheduleSpecialDays_Edit", PopulateAsync).Location("Content:1%Holidays and special days;3"));
    }

    public override async Task<IDisplayResult> UpdateAsync(BusinessHoursSchedule schedule, UpdateEditorContext context)
    {
        var model = new BusinessHoursScheduleViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var invalidDays = new List<DayOfWeek>();
        var invalidLines = new List<int>();

        schedule.Name = model.Name?.Trim();
        schedule.Description = model.Description?.Trim();
        schedule.Enabled = model.Enabled;
        schedule.Hours ??= new HoursSchedule();
        schedule.Hours.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? null : model.TimeZoneId.Trim();
        schedule.Hours.Windows = WeeklyHoursRows.ToWindows(model.Days, invalidDays);
        schedule.Hours.DateExceptions = HoursDateExceptionText.Parse(model.SpecialDays, invalidLines);

        foreach (var day in invalidDays.Distinct())
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(BusinessHoursScheduleViewModel.Days), S["{0}: enter the opening and closing times as hours and minutes, such as 09:00.", CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day)]);
        }

        foreach (var line in invalidLines)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(BusinessHoursScheduleViewModel.SpecialDays), S["Line {0} of the special days is not a date in the form 2026-12-25, optionally followed by hours such as 09:00-13:00 and a name.", line]);
        }

        return Edit(schedule, context);
    }
}
