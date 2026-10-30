using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Details card of office calendars.
/// </summary>
internal sealed class OfficeCalendarSettingsDisplayDriver : DisplayDriver<Calendar>
{
    public override IDisplayResult Edit(Calendar calendar, BuildEditorContext context)
    {
        if (!string.Equals(calendar.Type, CalendarsConstants.Types.Office, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var settings = calendar.GetOrCreate<OfficeCalendarSettings>();

        return Initialize<OfficeCalendarSettingsViewModel>("OfficeCalendarSettings_Edit", model =>
        {
            model.Address = settings.Address;
            model.Contact = settings.Contact;
        }).Location("Content:3%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        if (!string.Equals(calendar.Type, CalendarsConstants.Types.Office, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new OfficeCalendarSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        calendar.Put(new OfficeCalendarSettings
        {
            Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
            Contact = string.IsNullOrWhiteSpace(model.Contact) ? null : model.Contact.Trim(),
        });

        return Edit(calendar, context);
    }
}
