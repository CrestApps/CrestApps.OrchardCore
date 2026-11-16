using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Puts a staff member's class meetings on their shift plan while their place is confirmed, as Training segments on the
/// published shifts that cover them, and takes them off once the place is cancelled. It acts on the registration as it
/// is, so running it again (after a busy shift plan) does what is still needed.
/// </summary>
internal sealed class ClassShiftSegmentPlanner
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public ClassShiftSegmentPlanner(IServiceProvider services, ILogger<ClassShiftSegmentPlanner> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Places or removes the class meetings of a registration.
    /// </summary>
    /// <param name="appointment">The registration.</param>
    /// <param name="actor">The user id of the person whose change it follows.</param>
    /// <param name="remove">Whether the place was cancelled; <see langword="null"/> decides from the registration's status.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What became of the work.</returns>
    public async Task<ClassFollowUpOutcome> ApplyAsync(Appointment appointment, string actor, bool? remove = null, CancellationToken cancellationToken = default)
    {
        if (appointment is not { Source: ClassesConstants.RegistrationSource } || string.IsNullOrEmpty(appointment.UserId))
        {
            return ClassFollowUpOutcome.Done;
        }

        var worker = await _services.GetRequiredService<IStaffDirectory>().FindByUserIdAsync(appointment.UserId, cancellationToken);

        if (worker is null)
        {
            return ClassFollowUpOutcome.Done;
        }

        // The services are resolved when needed: the session service tells the observers about its changes.
        var session = string.IsNullOrEmpty(appointment.SessionId)
            ? null
            : await _services.GetRequiredService<ISchedulingSessionService>().FindByIdAsync(appointment.SessionId, cancellationToken);
        var meetings = session?.Meetings is { Count: > 0 } found
            ? found.Select(meeting => (meeting.StartUtc, meeting.EndUtc)).ToArray()
            : [(appointment.StartUtc, appointment.EndUtc)];
        var shifts = _services.GetRequiredService<IShiftService>();

        if (remove ?? appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Expired)
        {
            await shifts.RemoveSegmentsAsync(worker.ItemId, ClassesConstants.RegistrationSource, appointment.ItemId, meetings.Min(meeting => meeting.StartUtc), meetings.Max(meeting => meeting.EndUtc), actor, cancellationToken);

            return ClassFollowUpOutcome.Done;
        }

        if (remove is null && appointment.Status != AppointmentStatus.Confirmed)
        {
            return ClassFollowUpOutcome.Done;
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
                Actor = actor,
            }, cancellationToken);

            if (!placed.Succeeded && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The class meeting of registration {AppointmentId} was not put on the shift plan of staff member {WorkerId}: {Reason}", appointment.ItemId, worker.ItemId, string.Join(" ", placed.Errors));
            }
        }

        return ClassFollowUpOutcome.Done;
    }
}
