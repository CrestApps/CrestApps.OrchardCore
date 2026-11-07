using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// Lists the Scheduling appointment types for the work order type editor.
/// </summary>
internal sealed class SchedulingWorkOrderLookup : IWorkOrderLookup
{
    private readonly AppointmentTypeStore _types;

    public SchedulingWorkOrderLookup(AppointmentTypeStore types)
    {
        _types = types;
    }

    public async Task<IReadOnlyList<SelectListItem>> AppointmentTypesAsync(CancellationToken cancellationToken = default)
        => [.. (await _types.GetAllAsync(cancellationToken)).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => new SelectListItem(type.Name, type.ItemId))];
}

/// <summary>
/// Keeps a work order for each confirmed booking of an appointment type a work order type is made from: made when
/// the booking is confirmed, moved when it is rescheduled, and canceled with it.
/// </summary>
internal sealed class AppointmentWorkOrderObserver : IAppointmentObserver
{
    private readonly WorkOrderService _workOrders;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public AppointmentWorkOrderObserver(WorkOrderService workOrders, WorkOrderSettingManager<WorkOrderType> types, ISession session, IStringLocalizer<AppointmentWorkOrderObserver> stringLocalizer)
    {
        _workOrders = workOrders;
        _types = types;
        _session = session;
        S = stringLocalizer;
    }

    public async Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        var appointment = change?.Appointment;

        if (appointment is null || change.Kind is not (AppointmentChangeKind.Confirmed or AppointmentChangeKind.Rescheduled or AppointmentChangeKind.Cancelled))
        {
            return;
        }

        var existing = await _session.Query<WorkOrder, WorkOrderIndex>(index => index.Source == nameof(WorkOrderSource.Appointment) && index.SourceId == appointment.ItemId, collection: WorkOrdersConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
        var actor = change.Actor ?? appointment.CustomerName;

        if (change.Kind == AppointmentChangeKind.Cancelled)
        {
            if (existing is not null && WorkOrderStatusRules.CanMove(existing.Status, WorkOrderStatus.Canceled))
            {
                await _workOrders.ChangeStatusAsync(existing.ItemId, WorkOrderStatus.Canceled, null, string.IsNullOrWhiteSpace(appointment.CancelReason) ? S["The booking was canceled."] : appointment.CancelReason, actor, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (existing is not null)
        {
            // A rescheduled booking moves the work order's window; a visit already given a time stays with dispatch.
            var latest = LatestUtc(appointment, existing.EstimatedMinutes);

            if (existing.EarliestUtc != appointment.StartUtc || existing.LatestUtc != latest)
            {
                existing.EarliestUtc = appointment.StartUtc;
                existing.LatestUtc = latest;
                await _workOrders.UpdateAsync(existing, actor, cancellationToken);
                await _workOrders.AddNoteAsync(existing.ItemId, S["The booking moved to {0:g}.", CalendarTime.ToLocal(appointment.StartUtc, appointment.TimeZoneId)], actor, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var type = (await _types.GetAllAsync(cancellationToken)).FirstOrDefault(item => item.AppointmentTypeIds.Contains(appointment.AppointmentTypeId));

        if (type is null)
        {
            return;
        }

        var workOrder = await _workOrders.NewAsync(type.ItemId, cancellationToken);

        workOrder.Summary = change.AppointmentType?.Name ?? type.Name;
        workOrder.Description = appointment.Notes;
        workOrder.Contact = new WorkOrderContact { Name = appointment.CustomerName, Phone = appointment.CustomerPhone, Email = appointment.CustomerEmail };
        workOrder.EarliestUtc = appointment.StartUtc;
        workOrder.LatestUtc = LatestUtc(appointment, workOrder.EstimatedMinutes > 0 ? workOrder.EstimatedMinutes : type.DefaultDurationMinutes);
        workOrder.Source = WorkOrderSource.Appointment;
        workOrder.SourceId = appointment.ItemId;

        if ((await _workOrders.CreateAsync(workOrder, actor, cancellationToken)).Errors.Count == 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gives the end of a booked work order's window. The booked time is when the technician arrives, so the job may
    /// start as late as the booking's end and still needs its own length after that.
    /// </summary>
    /// <param name="appointment">The booking.</param>
    /// <param name="jobMinutes">The job's length.</param>
    /// <returns>The window's end, in UTC.</returns>
    internal static DateTime LatestUtc(Appointment appointment, int jobMinutes)
        => appointment.EndUtc.AddMinutes(Math.Max(0, jobMinutes));
}
