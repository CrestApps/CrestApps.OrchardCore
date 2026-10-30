using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Details card of room and equipment calendars: an example of a calendar type adding its own settings.
/// </summary>
internal sealed class ResourceCalendarSettingsDisplayDriver : DisplayDriver<Calendar>
{
    public override IDisplayResult Edit(Calendar calendar, BuildEditorContext context)
    {
        if (!IsResource(calendar))
        {
            return null;
        }

        var settings = calendar.GetOrCreate<ResourceCalendarSettings>();

        return Initialize<ResourceCalendarSettingsViewModel>("ResourceCalendarSettings_Edit", model =>
        {
            model.Location = settings.Location;
            model.Seats = settings.Seats;
            model.Amenities = settings.Amenities;
        }).Location("Content:3%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        if (!IsResource(calendar))
        {
            return null;
        }

        var model = new ResourceCalendarSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        calendar.Put(new ResourceCalendarSettings
        {
            Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim(),
            Seats = Math.Max(0, model.Seats),
            Amenities = string.IsNullOrWhiteSpace(model.Amenities) ? null : model.Amenities.Trim(),
        });

        return Edit(calendar, context);
    }

    private static bool IsResource(Calendar calendar)
    {
        return string.Equals(calendar.Type, CalendarsConstants.Types.Room, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(calendar.Type, CalendarsConstants.Types.Equipment, StringComparison.OrdinalIgnoreCase);
    }
}
