using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Puts a staff member's class on their shift plan when Workforce is on: each meeting of the session becomes a
/// Training segment on the published shift that covers it once their place is confirmed, and the segments go away when
/// the place is cancelled. A learner who isn't staff, or isn't working then, is left alone. Rescheduling a session's
/// meetings doesn't move segments already placed.
/// </summary>
internal sealed class ClassShiftSegmentObserver : IAppointmentObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public ClassShiftSegmentObserver(IServiceProvider services, ILogger<ClassShiftSegmentObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        if (change?.Kind is not (AppointmentChangeKind.Confirmed or AppointmentChangeKind.Cancelled) ||
            change.Appointment is not { Source: ClassesConstants.RegistrationSource } appointment ||
            string.IsNullOrEmpty(appointment.UserId))
        {
            return;
        }

        var worker = await _services.GetRequiredService<IStaffDirectory>().FindByUserIdAsync(appointment.UserId, cancellationToken);

        if (worker is null)
        {
            return;
        }

        // The services are resolved when needed: the session service tells this observer about its changes.
        var session = string.IsNullOrEmpty(appointment.SessionId)
            ? null
            : await _services.GetRequiredService<ISchedulingSessionService>().FindByIdAsync(appointment.SessionId, cancellationToken);
        var meetings = session?.Meetings is { Count: > 0 } found
            ? found.Select(meeting => (meeting.StartUtc, meeting.EndUtc)).ToArray()
            : [(appointment.StartUtc, appointment.EndUtc)];
        var shifts = _services.GetRequiredService<IShiftService>();

        if (change.Kind == AppointmentChangeKind.Cancelled)
        {
            await shifts.RemoveSegmentsAsync(worker.ItemId, ClassesConstants.RegistrationSource, appointment.ItemId, meetings.Min(meeting => meeting.StartUtc), meetings.Max(meeting => meeting.EndUtc), change.Actor, cancellationToken);

            return;
        }

        foreach (var (startUtc, endUtc) in meetings)
        {
            var placed = await shifts.PlaceSegmentAsync(new ShiftSegmentPlacement
            {
                WorkerId = worker.ItemId,
                Kind = ActivityKind.Training,
                StartUtc = startUtc,
                EndUtc = endUtc,
                Source = ClassesConstants.RegistrationSource,
                SourceId = appointment.ItemId,
                Reason = $"Class: {session?.Title}",
                Actor = change.Actor,
            }, cancellationToken);

            if (!placed.Succeeded && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The class meeting of registration {AppointmentId} was not put on the shift plan of staff member {WorkerId}: {Reason}", appointment.ItemId, worker.ItemId, string.Join(" ", placed.Errors));
            }
        }
    }
}
