using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The editor of the page that imports an .ics file: the file to choose. The file posts as <c>file</c>, which the
/// action reads, so the driver binds nothing.
/// </summary>
internal sealed class CalendarImportDisplayDriver : DisplayDriver<CalendarFeedViewModel>
{
    internal readonly IStringLocalizer S;

    public CalendarImportDisplayDriver(IStringLocalizer<CalendarImportDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CalendarFeedViewModel model, BuildEditorContext context)
    {
        return Initialize<CalendarFeedViewModel>("CalendarImport_Edit", m =>
        {
            m.Name = model.Name;
        }).Location(l => l.Zone("Content", "1").Card(S["File"], "10"));
    }

    public override Task<IDisplayResult> UpdateAsync(CalendarFeedViewModel model, UpdateEditorContext context)
        => Task.FromResult(Edit(model, context));
}
