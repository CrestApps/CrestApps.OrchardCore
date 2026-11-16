using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Completes a class item when its learner attended: attended, or partly attended for at least the class's minimum
/// minutes (see <see cref="ClassAttendanceRecorder"/>). Attendance that cannot be recorded now (a busy or suspended
/// enrollment, a failure) is queued and recorded again later. Recording the same attendance again changes nothing,
/// because a completed item stays completed.
/// </summary>
internal sealed class ClassAttendanceObserver : IAppointmentObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public ClassAttendanceObserver(IServiceProvider services, ILogger<ClassAttendanceObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        if (change?.Kind != AppointmentChangeKind.Completed ||
            change.Appointment is not { Source: ClassesConstants.RegistrationSource } appointment ||
            !ClassesConstants.TryReadRegistrationSourceId(appointment.SourceId, out _, out _))
        {
            return;
        }

        ClassFollowUpOutcome outcome;

        try
        {
            outcome = await _services.GetRequiredService<ClassAttendanceRecorder>().ApplyAsync(appointment, change.Actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            outcome = ClassFollowUpOutcome.Retry(exception.Message);
        }

        if (outcome.Kind == ClassFollowUpOutcomeKind.Done)
        {
            return;
        }

        _logger.LogWarning("The attendance of class registration {AppointmentId} was not recorded on the class item: {Reason}.", appointment.ItemId, outcome.Message);

        if (outcome.Kind == ClassFollowUpOutcomeKind.Retry)
        {
            await _services.GetRequiredService<ClassFollowUps>().EnqueueAsync(ClassFollowUpKinds.Attendance, appointment.ItemId, change.Actor, outcome.Message, cancellationToken);
        }
    }
}

/// <summary>
/// Gives up an enrollment's class places when it ends without being completed, so the next person waiting is offered
/// the place.
/// </summary>
internal sealed class ClassEnrollmentObserver : IEnrollmentObserver
{
    private readonly IServiceProvider _services;

    public ClassEnrollmentObserver(IServiceProvider services)
    {
        _services = services;
    }

    public async Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
    {
        if (enrollment is null || enrollment.IsOpen || enrollment.Status == EnrollmentStatus.Completed)
        {
            return;
        }

        await _services.GetRequiredService<IClassRegistrationService>().CancelAllAsync(enrollment.ItemId, "The enrollment ended.", "Learning", cancellationToken);
    }
}
