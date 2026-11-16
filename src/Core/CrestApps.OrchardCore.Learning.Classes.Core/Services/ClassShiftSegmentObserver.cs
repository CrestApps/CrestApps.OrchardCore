using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Puts a staff member's class on their shift plan when Workforce is on: each meeting of the session becomes a
/// Training segment on the published shift that covers it once their place is confirmed, and the segments go away when
/// the place is cancelled (see <see cref="ClassShiftSegmentPlanner"/>). A shift plan that is busy, or any other failure,
/// queues the work to be done again. A learner who isn't staff, or isn't working then, is left alone. Rescheduling a
/// session's meetings doesn't move segments already placed.
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

        try
        {
            await _services.GetRequiredService<ClassShiftSegmentPlanner>().ApplyAsync(appointment, change.Actor, change.Kind == AppointmentChangeKind.Cancelled, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The class meetings of registration {AppointmentId} were not changed on the shift plan and are tried again.", appointment.ItemId);

            await _services.GetRequiredService<ClassFollowUps>().EnqueueAsync(ClassFollowUpKinds.ShiftSegments, appointment.ItemId, change.Actor, exception.Message, cancellationToken);
        }
    }
}
