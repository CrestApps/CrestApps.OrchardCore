using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Overlaps card of the calendar editor: whether events may overlap, and how many at once.
/// </summary>
internal sealed class CalendarRulesDisplayDriver : DisplayDriver<Calendar>
{
    internal readonly IStringLocalizer S;

    public CalendarRulesDisplayDriver(IStringLocalizer<CalendarRulesDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(Calendar calendar, BuildEditorContext context)
    {
        return Initialize<CalendarRulesViewModel>("CalendarRules_Edit", model =>
        {
            model.LimitOverlaps = calendar.ConflictPolicy == CalendarConflictPolicy.LimitConcurrent;
            model.Capacity = Math.Max(1, calendar.Capacity);
        }).Location("Content:15%Overlaps;4");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        var model = new CalendarRulesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Capacity < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Capacity), S["The number of events that may overlap must be at least 1."]);
        }

        calendar.ConflictPolicy = model.LimitOverlaps ? CalendarConflictPolicy.LimitConcurrent : CalendarConflictPolicy.AllowOverlaps;
        calendar.Capacity = Math.Max(1, model.Capacity);

        return Edit(calendar, context);
    }
}
