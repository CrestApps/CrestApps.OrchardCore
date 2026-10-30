using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Scheduling.Drivers;

/// <summary>
/// The rows of the appointment list.
/// </summary>
internal sealed class AppointmentDisplayDriver : DisplayDriver<Appointment>
{
    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly SchedulingPageService _pageService;

    public AppointmentDisplayDriver(
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        SchedulingPageService pageService)
    {
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        _pageService = pageService;
    }

    public override async Task<IDisplayResult> DisplayAsync(Appointment appointment, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId);
        var resources = await _resourceStore.GetAsync(appointment.ResourceIds);
        var timeZoneId = await _pageService.GetTimeZoneIdAsync();

        void Populate(AppointmentRowViewModel model)
        {
            model.Appointment = appointment;
            model.TypeName = type?.Name;
            model.ResourceNames = string.Join(", ", resources.Select(resource => resource.Name));
            model.LocalStart = CalendarTime.ToLocal(appointment.StartUtc, timeZoneId);
        }

        return Combine(
            Initialize<AppointmentRowViewModel>("Appointment_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<AppointmentRowViewModel>("Appointment_Tags_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            Initialize<AppointmentRowViewModel>("Appointment_Buttons_SummaryAdmin", Populate).Location("Actions:5"));
    }
}

/// <summary>
/// The rows of the appointment type list.
/// </summary>
internal sealed class AppointmentTypeDisplayDriver : DisplayDriver<AppointmentType>
{
    public override IDisplayResult Display(AppointmentType type, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("AppointmentType_Fields_SummaryAdmin", type)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AppointmentType_Tags_SummaryAdmin", type)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("AppointmentType_Buttons_SummaryAdmin", type).Location("Actions:5"),
            View("AppointmentType_ActionsMenu_SummaryAdmin", type).Location("ActionsMenu:10"));
    }
}

/// <summary>
/// The rows of the resource list.
/// </summary>
internal sealed class SchedulingResourceDisplayDriver : DisplayDriver<SchedulingResource>
{
    public override IDisplayResult Display(SchedulingResource resource, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("SchedulingResource_Fields_SummaryAdmin", resource)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SchedulingResource_Tags_SummaryAdmin", resource)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("SchedulingResource_Buttons_SummaryAdmin", resource).Location("Actions:5"),
            View("SchedulingResource_ActionsMenu_SummaryAdmin", resource).Location("ActionsMenu:10"));
    }
}

/// <summary>
/// The rows of the waitlist.
/// </summary>
internal sealed class WaitlistEntryDisplayDriver : DisplayDriver<WaitlistEntry>
{
    public override IDisplayResult Display(WaitlistEntry entry, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("WaitlistEntry_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("WaitlistEntry_Tags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("WaitlistEntry_ActionsMenu_SummaryAdmin", entry).Location("ActionsMenu:10"));
    }
}
