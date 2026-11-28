using CrestApps.Core.AI;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Scheduling.AI;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Registers the scheduling AI tools, which administrators can pick on AI profiles, whenever the AI feature is on.
/// </summary>
[Feature(SchedulingConstants.Features.Scheduling)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAppointmentTypesTool>(ListAppointmentTypesTool.ToolName).WithTitle(S["List appointment types"]).WithDescription(S["Lists the kinds of appointments that can be booked."]).WithCategory(S["Scheduling"]).Selectable();
        services.AddCoreAITool<FindAppointmentSlotsTool>(FindAppointmentSlotsTool.ToolName).WithTitle(S["Find appointment slots"]).WithDescription(S["Finds open times for an appointment."]).WithCategory(S["Scheduling"]).Selectable();
        services.AddCoreAITool<BookAppointmentTool>(BookAppointmentTool.ToolName).WithTitle(S["Book appointment"]).WithDescription(S["Books an appointment for the customer."]).WithCategory(S["Scheduling"]).Selectable();
        services.AddCoreAITool<ChangeAppointmentTool>(ChangeAppointmentTool.ToolName).WithTitle(S["Change appointment"]).WithDescription(S["Cancels or moves a customer's appointment."]).WithCategory(S["Scheduling"]).Selectable();
    }
}

/// <summary>
/// Registers the scheduling administration AI tools: appointments and their status, appointment types and the waitlist.
/// They work only for a signed-in person with the permission of the matching admin page. The scheduling resources are the
/// <c>schedulingResource</c> kind of the generic record tools (<c>listRecords</c>, <c>getRecord</c>, <c>saveRecord</c> and
/// <c>deleteRecord</c>), whose writes replay the resource editor of the Scheduling Resources page. Appointment types keep
/// their own tools: their editor's resource groups are a list of objects the record tools cannot post.
/// <c>setAppointmentStatus</c> stays apart from the customer-facing <c>changeAppointment</c>: approval is set per tool, and
/// confirming or marking an appointment completed or no-show asks for it, while a customer's cancel or move does not.
/// </summary>
[Feature(SchedulingConstants.Features.Scheduling)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class SchedulingAdminAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingAdminAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchedulingAdminAIToolsStartup(IStringLocalizer<SchedulingAdminAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAppointmentsTool>(ListAppointmentsTool.ToolName)
            .WithTitle(S["List appointments"])
            .WithDescription(S["Lists appointments in start time order, filtered by status, a search on the customer's name, email or phone, and a start date, with paging. Use it to find an appointment's id."])
            .WithCategory(S["Scheduling"])
            .Selectable();

        services.AddCoreAITool<GetAppointmentTool>(GetAppointmentTool.ToolName)
            .WithTitle(S["Get appointment"])
            .WithDescription(S["Gets one appointment: type, time, status, resources, customer, notes, cancel reason and the calendar event it sits on."])
            .WithCategory(S["Scheduling"])
            .Selectable();

        services.AddCoreAITool<SetAppointmentStatusTool>(SetAppointmentStatusTool.ToolName)
            .WithTitle(S["Set appointment status"])
            .WithDescription(S["Confirms a requested appointment, or marks a past one completed or no-show. Use changeAppointment to move or cancel."])
            .WithCategory(S["Scheduling"])
            .Selectable()
            .AsWrite()
            .RequireApproval();

        services.AddCoreAITool<GetAppointmentTypeTool>(GetAppointmentTypeTool.ToolName)
            .WithTitle(S["Get appointment type"])
            .WithDescription(S["Gets an appointment type in full: timing, limits, notice rules, capacity, resource groups with their assignment rules, self-service and its public booking address."])
            .WithCategory(S["Scheduling"])
            .Selectable();

        services.AddCoreAITool<SaveAppointmentTypeTool>(SaveAppointmentTypeTool.ToolName)
            .WithTitle(S["Save appointment type"])
            .WithDescription(S["Creates or updates an appointment type: name, slug, duration, slot interval, buffers, notice, horizon, daily and weekly limits, capacity, confirmation, hold time, self-service, location, color, enabled and the groups of resources it needs. A resource can be in only one group."])
            .WithCategory(S["Scheduling"])
            .Selectable();

        services.AddCoreAITool<DeleteAppointmentTypeTool>(DeleteAppointmentTypeTool.ToolName)
            .WithTitle(S["Delete appointment type"])
            .WithDescription(S["Deletes an appointment type. Appointments already booked keep pointing at it, so turn the type off instead when it has appointments."])
            .WithCategory(S["Scheduling"])
            .Selectable()
            .AsDestructive();

        services.AddCoreAITool<ListAppointmentWaitlistTool>(ListAppointmentWaitlistTool.ToolName)
            .WithTitle(S["List appointment waitlist"])
            .WithDescription(S["Lists the customers waiting for an opening, with the appointment type, the days they can come and their status."])
            .WithCategory(S["Scheduling"])
            .Selectable();

        services.AddCoreAITool<AddToAppointmentWaitlistTool>(AddToAppointmentWaitlistTool.ToolName)
            .WithTitle(S["Add to appointment waitlist"])
            .WithDescription(S["Adds a customer to the waitlist of an appointment type for a range of days. They are told when a time opens (through the Waitlist Slot Available workflow event)."])
            .WithCategory(S["Scheduling"])
            .Selectable();

        services.AddCoreAITool<RemoveFromAppointmentWaitlistTool>(RemoveFromAppointmentWaitlistTool.ToolName)
            .WithTitle(S["Remove from appointment waitlist"])
            .WithDescription(S["Takes a customer off the waitlist. The entry is kept with status Removed."])
            .WithCategory(S["Scheduling"])
            .Selectable();

        // The store is not a catalog manager, so every operation is the page's own: weekday hours on a new resource, and
        // its Appointments calendar on save. The page checks nothing before a delete; the calendar and appointments stay.
        services.AddAIRecordKind<SchedulingResource>(SchedulingResourceRecordKind.Name, kind => kind
            .WithTitle(S["Scheduling resource"])
            .WithDescription(S["a staff member, room, piece of equipment or other resource that takes appointments, with its calendar, time zone and bookable hours. Kind is Staff, Room, Equipment or Other; AvailabilityMode is WorkingHours, or Shifts to follow the ShiftCalendarIds; WorkingHours is one line per day, such as \"Monday 09:00-12:00, 13:00-17:00\" (a day left out is closed); DateOverrides is one line per date, such as \"2026-12-24 closed\" or \"2026-12-31 08:00-12:00\"; times are wall-clock times in the resource's time zone. An empty CalendarId gets the resource its own Appointments calendar; a delete keeps the calendar and appointments"])
            .WithPermission(SchedulingPermissions.ManageScheduling)
            .FindUsing(SchedulingResourceRecordKind.FindAsync)
            .NewUsing((_, _) => ValueTask.FromResult(SchedulingResourceEditorRules.NewResource()))
            .ListUsing(SchedulingResourceRecordKind.ListAsync)
            .ValidateUsing((_, _, _) => ValueTask.FromResult(new ValidationResultDetails()))
            .SaveUsing(SchedulingResourceRecordKind.SaveAsync)
            .DeleteUsing(SchedulingResourceRecordKind.DeleteAsync)
            .WithProjection(SchedulingResourceRecordKind.Project)
            .UseEditorReplay());
    }
}
