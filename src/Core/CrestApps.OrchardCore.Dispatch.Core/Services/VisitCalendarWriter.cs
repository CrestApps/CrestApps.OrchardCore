using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// What a job event on a team calendar says about its visit.
/// </summary>
public sealed class VisitEventData
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public string WorkOrderId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }
}

/// <summary>
/// Keeps a job event on each assigned worker's team calendar, so their schedule shows the visit and they are busy
/// then.
/// </summary>
public sealed class VisitCalendarWriter
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisitCalendarWriter"/> class.
    /// </summary>
    public VisitCalendarWriter(ICalendarService calendarService, ICalendarEventManager eventManager)
    {
        _calendarService = calendarService;
        _eventManager = eventManager;
    }

    /// <summary>
    /// Makes, moves or removes the visit's job events to match its workers and time.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="workers">The assigned workers with their calendars.</param>
    /// <param name="title">The events' title.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The errors the calendars gave, if any.</returns>
    public async Task<IReadOnlyList<string>> SyncAsync(ServiceVisit visit, IReadOnlyList<WorkerAvailability> workers, string title, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var errors = new List<string>();
        var options = new CalendarChangeOptions { Actor = actor, Notify = false, SendInvitations = false, SkipConflictCheck = true };
        var keep = new List<VisitCalendarEvent>();

        foreach (var availability in workers ?? [])
        {
            var worker = availability.Worker;
            var existing = visit.CalendarEvents.FirstOrDefault(entry => entry.ResourceKind == DispatchConstants.ResourceKinds.Worker && entry.ResourceId == worker.ItemId);
            var calendarEvent = existing is null ? null : await _eventManager.FindByIdAsync(existing.EventId, cancellationToken);

            if (calendarEvent is not null && calendarEvent.CalendarId == availability.CalendarId)
            {
                var updated = await _eventManager.UpdateAsync(calendarEvent, change => Apply(change, visit, availability, title), options, cancellationToken);

                errors.AddRange(updated.Errors.Select(error => error.Message));
                keep.Add(existing);

                continue;
            }

            if (calendarEvent is not null)
            {
                await _eventManager.DeleteAsync(calendarEvent, options, cancellationToken);
            }

            var calendar = string.IsNullOrEmpty(availability.CalendarId) ? null : await _calendarService.FindByIdAsync(availability.CalendarId, cancellationToken);

            // A worker in no team has no calendar for the job; the visit still names them.
            if (calendar is null)
            {
                continue;
            }

            var created = await _eventManager.NewAsync(calendar, DispatchConstants.JobEventType, visit.StartUtc, cancellationToken);

            created.Source = DispatchConstants.Source;
            created.SourceId = visit.ItemId;
            created.Editable = false;
            Apply(created, visit, availability, title);

            var result = await _eventManager.CreateAsync(created, options, cancellationToken);

            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors.Select(error => error.Message));

                continue;
            }

            keep.Add(new VisitCalendarEvent { ResourceKind = DispatchConstants.ResourceKinds.Worker, ResourceId = worker.ItemId, EventId = result.Value.ItemId });
        }

        foreach (var stale in visit.CalendarEvents.Where(entry => entry.ResourceKind == DispatchConstants.ResourceKinds.Worker && !keep.Contains(entry)))
        {
            var calendarEvent = await _eventManager.FindByIdAsync(stale.EventId, cancellationToken);

            if (calendarEvent is not null)
            {
                await _eventManager.DeleteAsync(calendarEvent, options, cancellationToken);
            }
        }

        visit.CalendarEvents = [.. keep, .. visit.CalendarEvents.Where(entry => entry.ResourceKind != DispatchConstants.ResourceKinds.Worker)];

        return errors;
    }

    /// <summary>
    /// Books the visit's equipment on each one's calendar, moving or removing what it had.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="equipment">The equipment it has now.</param>
    /// <param name="title">The events' title.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The errors the calendars gave, if any.</returns>
    public async Task<IReadOnlyList<string>> SyncEquipmentAsync(ServiceVisit visit, IReadOnlyList<DispatchEquipment> equipment, string title, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var errors = new List<string>();
        var options = new CalendarChangeOptions { Actor = actor, Notify = false, SendInvitations = false };
        var keep = new List<VisitCalendarEvent>();

        foreach (var item in equipment ?? [])
        {
            var existing = visit.CalendarEvents.FirstOrDefault(entry => entry.ResourceKind == DispatchConstants.ResourceKinds.Equipment && entry.ResourceId == item.AssetId);
            var calendarEvent = existing is null ? null : await _eventManager.FindByIdAsync(existing.EventId, cancellationToken);

            if (calendarEvent is not null)
            {
                var updated = await _eventManager.UpdateAsync(calendarEvent, change => ApplyEquipment(change, visit, title), options, cancellationToken);

                errors.AddRange(updated.Errors.Select(error => error.Message));
                keep.Add(existing);

                continue;
            }

            var calendar = await _calendarService.FindByIdAsync(item.CalendarId, cancellationToken);

            if (calendar is null)
            {
                continue;
            }

            var created = await _eventManager.NewAsync(calendar, EquipmentEventType, visit.StartUtc, cancellationToken);

            created.Source = DispatchConstants.Source;
            created.SourceId = visit.ItemId;
            created.Editable = false;
            ApplyEquipment(created, visit, title);

            var result = await _eventManager.CreateAsync(created, options, cancellationToken);

            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors.Select(error => error.Message));

                continue;
            }

            keep.Add(new VisitCalendarEvent { ResourceKind = DispatchConstants.ResourceKinds.Equipment, ResourceId = item.AssetId, EventId = result.Value.ItemId });
        }

        foreach (var stale in visit.CalendarEvents.Where(entry => entry.ResourceKind == DispatchConstants.ResourceKinds.Equipment && !keep.Contains(entry)))
        {
            var calendarEvent = await _eventManager.FindByIdAsync(stale.EventId, cancellationToken);

            if (calendarEvent is not null)
            {
                await _eventManager.DeleteAsync(calendarEvent, options, cancellationToken);
            }
        }

        visit.CalendarEvents = [.. visit.CalendarEvents.Where(entry => entry.ResourceKind != DispatchConstants.ResourceKinds.Equipment), .. keep];
        visit.EquipmentAssetIds = [.. keep.Select(entry => entry.ResourceId)];

        return errors;
    }

    /// <summary>
    /// The event type a visit's equipment is booked with on its Equipment calendar.
    /// </summary>
    public const string EquipmentEventType = "Reservation";

    private static void ApplyEquipment(CalendarEvent calendarEvent, ServiceVisit visit, string title)
    {
        calendarEvent.Title = title;
        calendarEvent.AllDay = false;
        calendarEvent.TimeZoneId = "UTC";
        calendarEvent.Start = DateTime.SpecifyKind(visit.StartUtc.Value, DateTimeKind.Unspecified);
        calendarEvent.End = DateTime.SpecifyKind(visit.EndUtc.Value, DateTimeKind.Unspecified);
        calendarEvent.Transparency = CalendarTransparency.Busy;
        calendarEvent.Put(new VisitEventData { VisitId = visit.ItemId, WorkOrderId = visit.WorkOrderId });
    }

    /// <summary>
    /// Removes all the visit's job events.
    /// </summary>
    /// <param name="visit">The visit.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RemoveAsync(ServiceVisit visit, string actor, CancellationToken cancellationToken = default)
    {
        await SyncAsync(visit, [], null, actor, cancellationToken);
        await SyncEquipmentAsync(visit, [], null, actor, cancellationToken);
    }

    private static void Apply(CalendarEvent calendarEvent, ServiceVisit visit, WorkerAvailability availability, string title)
    {
        var worker = availability.Worker;

        calendarEvent.Title = title;
        calendarEvent.AllDay = false;
        calendarEvent.TimeZoneId = availability.TimeZoneId;
        calendarEvent.Start = Calendars.CalendarTime.ToLocal(visit.StartUtc.Value, availability.TimeZoneId);
        calendarEvent.End = Calendars.CalendarTime.ToLocal(visit.EndUtc.Value, availability.TimeZoneId);
        calendarEvent.Transparency = CalendarTransparency.Busy;
        calendarEvent.Attendees = string.IsNullOrEmpty(worker.UserId) && string.IsNullOrEmpty(worker.Email)
            ? []
            :
            [
                new CalendarAttendee
                {
                    UserId = worker.UserId,
                    Email = worker.Email,
                    Name = worker.DisplayName,
                    Role = CalendarAttendeeRole.Required,
                    Response = CalendarAttendeeResponse.Accepted,
                },
            ];
        calendarEvent.Put(new VisitEventData { VisitId = visit.ItemId, WorkOrderId = visit.WorkOrderId, WorkerId = worker.ItemId });
    }
}
